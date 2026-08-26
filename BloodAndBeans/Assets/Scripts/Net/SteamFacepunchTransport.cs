using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using UnityEngine;

using SocketConnection = Steamworks.Data.Connection;

/// Steam Datagram Relay 위로 도는 NGO 트랜스포트. 클라이언트는 IP가 아니라 호스트의
/// SteamId로 붙는다.
///
/// 출처: multiplayer-community-contributions의 `com.community.netcode.transport.facepunch`
/// (MIT). 패키지를 그대로 쓰지 않고 프로젝트 소유 복사본으로 둔 이유는 두 가지다.
/// 1. 원본은 `Initialize`에서 무조건 `SteamClient.Init`을 부른다. 로비 목록은 접속 전에
///    떠야 하므로 `SteamLobby`가 이미 초기화해 둔 상태이고, 그러면 원본은 예외를 던지고
///    Console에 오류를 남긴다.
/// 2. 원본은 `Shutdown`에서 `SteamClient.Shutdown`까지 부른다. 매치를 끝내고 로비로
///    돌아오면 스팀 세션이 통째로 죽어 방 목록이 다시 뜨지 않는다.
/// 스팀 세션의 수명은 `SteamLobby`가 가진다. 이 클래스는 소켓만 연다.
///
/// 수신 복사는 원본의 `unsafe`+`UnsafeUtility.MemCpy` 대신 `Marshal.Copy`를 쓴다.
/// 같은 일을 하면서 어셈블리에 unsafe 허용을 켜지 않아도 된다.
public class SteamFacepunchTransport : NetworkTransport, IConnectionManager, ISocketManager
{
    [SerializeField] ulong targetSteamId;

    ConnectionManager connectionManager;
    SocketManager socketManager;
    readonly Dictionary<ulong, SocketConnection> connections = new();


    /// 클라이언트로 붙을 대상 호스트. `StartClient` 전에 로비가 채운다.
    public ulong TargetSteamId
    {
        get => targetSteamId;
        set => targetSteamId = value;
    }

    public override ulong ServerClientId => 0;

    LogLevel Verbosity => NetworkManager.Singleton != null ? NetworkManager.Singleton.LogLevel : LogLevel.Normal;

    /// 스팀 콜백 펌프. 평소에는 `SteamLobby`가 매 프레임 돌리지만, 로비 없이 트랜스포트만
    /// 살아 있는 경우에도 연결이 멈추지 않도록 여기서도 돌린다. 두 번 불러도 큐를 한 번 더
    /// 비울 뿐이다.
    protected override void OnEarlyUpdate()
    {
        if (SteamClient.IsValid) SteamClient.RunCallbacks();
    }

    public override void Initialize(NetworkManager networkManager = null)
    {
        connections.Clear();

        if (!SteamClient.IsValid)
        {
            Debug.LogError($"{nameof(SteamFacepunchTransport)}: 스팀이 초기화되지 않았다. "
                         + $"씬에 {nameof(SteamLobby)}가 있어야 하고, 스팀 클라이언트가 실행 중이어야 한다.", this);
            return;
        }

        SteamNetworkingUtils.InitRelayNetworkAccess();
    }

    public override bool StartServer()
    {
        if (!SteamClient.IsValid) return false;

        socketManager = SteamNetworkingSockets.CreateRelaySocket<SocketManager>();
        socketManager.Interface = this;
        return true;
    }

    public override bool StartClient()
    {
        if (!SteamClient.IsValid) return false;

        connectionManager = SteamNetworkingSockets.ConnectRelay<ConnectionManager>(targetSteamId);
        connectionManager.Interface = this;
        return true;
    }

    public override void Shutdown()
    {
        connectionManager?.Close();
        socketManager?.Close();
        connectionManager = null;
        socketManager = null;
        connections.Clear();
    }

    public override void Send(ulong clientId, ArraySegment<byte> payload, NetworkDelivery networkDelivery)
    {
        var sendType = ToSendType(networkDelivery);

        if (clientId == ServerClientId)
            connectionManager?.Connection.SendMessage(payload.Array, payload.Offset, payload.Count, sendType);
        else if (connections.TryGetValue(clientId, out var connection))
            connection.SendMessage(payload.Array, payload.Offset, payload.Count, sendType);
        else if (Verbosity <= LogLevel.Normal)
            Debug.LogWarning($"{nameof(SteamFacepunchTransport)}: 접속하지 않은 클라이언트 {clientId}에게 보내려 했다.", this);
    }

    /// 스팀 소켓은 콜백으로 밀어 넣으므로 폴링할 것이 없다. 받은 것은
    /// `InvokeOnTransportEvent`로 이미 올라간다.
    public override NetworkEvent PollEvent(out ulong clientId, out ArraySegment<byte> payload, out float receiveTime)
    {
        connectionManager?.Receive();
        socketManager?.Receive();

        clientId = 0;
        payload = default;
        receiveTime = Time.realtimeSinceStartup;
        return NetworkEvent.Nothing;
    }

    public override void DisconnectLocalClient() => connectionManager?.Connection.Close();

    public override void DisconnectRemoteClient(ulong clientId)
    {
        if (!connections.TryGetValue(clientId, out var connection)) return;

        connection.Flush();     // 끊기 전에 남은 메시지를 흘려보낸다
        connection.Close();
        connections.Remove(clientId);
    }

    /// SDR은 왕복 시간을 이 API로 내주지 않는다. 원본도 0을 돌려준다.
    public override ulong GetCurrentRtt(ulong clientId) => 0;

    static SendType ToSendType(NetworkDelivery delivery) => delivery switch
    {
        NetworkDelivery.Unreliable => SendType.Unreliable,
        NetworkDelivery.UnreliableSequenced => SendType.Unreliable,
        _ => SendType.Reliable
    };

    void Deliver(ulong clientId, IntPtr data, int size)
    {
        var payload = new byte[size];
        Marshal.Copy(data, payload, 0, size);
        InvokeOnTransportEvent(NetworkEvent.Data, clientId, new ArraySegment<byte>(payload, 0, size),
                               Time.realtimeSinceStartup);
    }

    // --- 클라이언트 쪽 연결 (호스트로 나가는 단 하나의 연결) ---

    void IConnectionManager.OnConnecting(ConnectionInfo info) { }

    void IConnectionManager.OnConnected(ConnectionInfo info) =>
        InvokeOnTransportEvent(NetworkEvent.Connect, ServerClientId, default, Time.realtimeSinceStartup);

    void IConnectionManager.OnDisconnected(ConnectionInfo info) =>
        InvokeOnTransportEvent(NetworkEvent.Disconnect, ServerClientId, default, Time.realtimeSinceStartup);

    void IConnectionManager.OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel) =>
        Deliver(ServerClientId, data, size);

    // --- 서버 쪽 소켓 (붙어 오는 모든 클라이언트) ---

    void ISocketManager.OnConnecting(SocketConnection connection, ConnectionInfo info) => connection.Accept();

    void ISocketManager.OnConnected(SocketConnection connection, ConnectionInfo info)
    {
        if (connections.ContainsKey(connection.Id)) return;

        connections.Add(connection.Id, connection);
        InvokeOnTransportEvent(NetworkEvent.Connect, connection.Id, default, Time.realtimeSinceStartup);
    }

    void ISocketManager.OnDisconnected(SocketConnection connection, ConnectionInfo info)
    {
        if (!connections.Remove(connection.Id)) return;

        InvokeOnTransportEvent(NetworkEvent.Disconnect, connection.Id, default, Time.realtimeSinceStartup);
    }

    void ISocketManager.OnMessage(SocketConnection connection, NetIdentity identity, IntPtr data, int size,
                                  long messageNum, long recvTime, int channel) =>
        Deliver(connection.Id, data, size);
}
