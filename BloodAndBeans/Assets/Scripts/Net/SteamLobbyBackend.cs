using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Steamworks;
using Steamworks.Data;

/// 스팀 로비 하나를 방으로 쓰는 백엔드. 스팀 SDK를 아는 유일한 자리다 — 방 흐름 쪽에서
/// `Steamworks`를 참조하기 시작하면 에디터 플랫폼에서 무엇이 안 도는지 알 수 없어진다.
public sealed class SteamLobbyBackend : ILobbyBackend
{
    /// 로비 메타데이터 키. 목록 질의 필터로도 쓰이므로 값이 바뀌면 구버전 방이 안 보인다.
    const string GameKey = "bb_game";
    const string GameValue = "blood-and-beans";
    const string NameKey = "bb_room";
    const string HostKey = "bb_host";

    /// 방을 만든 사람이 고른 팀 수 (기획서 10장). 적어 두지 않으면 참가자는 자기 씬의
    /// 기본값으로 좌석을 짜서, 호스트가 3팀으로 만든 방에 4팀 좌석표를 들고 들어온다.
    const string TeamsKey = "bb_teams";
    const string LiveKey = "bb_live";

    /// 멤버별 메타데이터 키. 대기실의 팀 선택이 여기로 오간다.
    const string TeamKey = "bb_team";
    const string ReadyKey = "bb_ready";
    const string PickKey = "bb_pick";

    readonly uint appId;

    /// 목록으로 받은 로비 핸들. `LobbyRoom`은 백엔드를 가리지 않는 요약이라 Facepunch
    /// 구조체를 싣지 않는다 — 참가할 때 id로 여기서 되찾는다.
    readonly List<Lobby> handles = new();

    readonly List<LobbyRoom> rooms = new();
    readonly List<LobbyMember> members = new();

    Lobby? current;
    bool ownsSession;
    bool subscribed;

    public SteamLobbyBackend(uint appId) => this.appId = appId;

    public bool Ready => SteamClient.IsValid;
    public ulong SelfId => Ready ? SteamClient.SteamId.Value : 0ul;
    public string SelfName => Ready ? SteamClient.Name : string.Empty;
    public string LastError { get; private set; } = string.Empty;

    public event Action Changed;
    public event Action<ulong> MatchStarted;

    public void Initialize()
    {
        Subscribe();
        if (SteamClient.IsValid) return;

        try
        {
            SteamClient.Init(appId, false);
            ownsSession = true;
            LastError = string.Empty;
        }
        catch (Exception e)
        {
            // 스팀이 꺼져 있거나 로그인되지 않은 상태다. 게임을 죽일 이유는 없고, 방 흐름만
            // 잠근 채 로컬 테스트 경로를 남겨 둔다.
            LastError = $"스팀에 연결하지 못했다: {e.Message}";
        }
    }

    /// 스팀 콜백 펌프. `SteamClient.Init(appId, asyncCallbacks: false)`로 열었으므로 이 호출이
    /// 없으면 로비 생성·목록·입장 콜백이 영원히 오지 않는다.
    public void Pump()
    {
        if (SteamClient.IsValid) SteamClient.RunCallbacks();
    }

    public void Shutdown()
    {
        Unsubscribe();
        current?.Leave();
        current = null;

        if (ownsSession && SteamClient.IsValid) SteamClient.Shutdown();
        ownsSession = false;
    }

    // --- 구독 ---

    /// 스팀 쪽 이벤트는 static이다. 해제하지 않으면 죽은 백엔드가 계속 불린다.
    void Subscribe()
    {
        if (subscribed) return;

        SteamMatchmaking.OnLobbyMemberJoined += OnMembershipChanged;
        SteamMatchmaking.OnLobbyMemberLeave += OnMembershipChanged;
        SteamMatchmaking.OnLobbyMemberDisconnected += OnMembershipChanged;
        SteamMatchmaking.OnLobbyMemberDataChanged += OnMemberDataChanged;
        SteamMatchmaking.OnLobbyDataChanged += OnLobbyDataChanged;
        SteamMatchmaking.OnLobbyGameCreated += OnHostStartedMatch;
        subscribed = true;
    }

    void Unsubscribe()
    {
        if (!subscribed) return;

        SteamMatchmaking.OnLobbyMemberJoined -= OnMembershipChanged;
        SteamMatchmaking.OnLobbyMemberLeave -= OnMembershipChanged;
        SteamMatchmaking.OnLobbyMemberDisconnected -= OnMembershipChanged;
        SteamMatchmaking.OnLobbyMemberDataChanged -= OnMemberDataChanged;
        SteamMatchmaking.OnLobbyDataChanged -= OnLobbyDataChanged;
        SteamMatchmaking.OnLobbyGameCreated -= OnHostStartedMatch;
        subscribed = false;
    }

    void OnMembershipChanged(Lobby lobby, Friend _) => RaiseIfCurrent(lobby);
    void OnMemberDataChanged(Lobby lobby, Friend _) => RaiseIfCurrent(lobby);
    void OnLobbyDataChanged(Lobby lobby) => RaiseIfCurrent(lobby);

    void RaiseIfCurrent(Lobby lobby)
    {
        if (!current.HasValue || lobby.Id != current.Value.Id) return;
        Changed?.Invoke();
    }

    void OnHostStartedMatch(Lobby lobby, uint ip, ushort port, SteamId server)
    {
        if (!current.HasValue || lobby.Id != current.Value.Id) return;
        if (!Ready || server.Value == SelfId) return;   // 호스트 자신

        // 붙을 곳은 방장이어야 한다. 스팀도 방 주인이 아닌 멤버의 SetLobbyGameServer를
        // 막지만, 손님이 어디로 접속할지를 이벤트 값 하나만 믿고 정하지는 않는다.
        if (server.Value != current.Value.Owner.Id)
        {
            LastError = "방장이 아닌 곳에서 시작 신호가 왔다. 접속하지 않는다.";
            Changed?.Invoke();
            return;
        }

        MatchStarted?.Invoke(server.Value);
    }

    // --- 방 목록 ---

    public async UniTask<IReadOnlyList<LobbyRoom>> ListRoomsAsync(int limit)
    {
        if (!Ready)
        {
            LastError = "스팀이 준비되지 않았다.";
            return null;
        }

        Lobby[] found;
        try
        {
            found = await SteamMatchmaking.LobbyList
                                          .WithMaxResults(limit)
                                          .WithKeyValue(GameKey, GameValue)
                                          .RequestAsync();
        }
        catch (Exception e)
        {
            LastError = $"방 목록을 받지 못했다: {e.Message}";
            return null;
        }

        handles.Clear();
        rooms.Clear();

        // 결과가 하나도 없으면 빈 배열이 아니라 null이 온다 (Facepunch LobbyQuery.RequestAsync).
        if (found != null)
            foreach (var lobby in found)
            {
                // 이미 시작한 방은 들어가 봐야 승인 전에 막힌다. 목록에서 뺀다.
                if (!string.IsNullOrEmpty(lobby.GetData(LiveKey))) continue;

                handles.Add(lobby);
                rooms.Add(Describe(lobby));
            }

        LastError = string.Empty;
        return rooms;
    }

    static LobbyRoom Describe(Lobby lobby)
    {
        var name = lobby.GetData(NameKey);
        ulong.TryParse(lobby.GetData(HostKey), out var host);
        return new LobbyRoom(lobby.Id.Value, host,
                             string.IsNullOrEmpty(name) ? lobby.Id.ToString() : name,
                             lobby.MemberCount, lobby.MaxMembers);
    }

    // --- 방 만들기 / 참가 ---

    public async UniTask<bool> CreateRoomAsync(string roomName, int capacity, int teamCount)
    {
        if (!Ready)
        {
            LastError = "스팀이 준비되지 않았다.";
            return false;
        }

        Lobby? created;
        try
        {
            created = await SteamMatchmaking.CreateLobbyAsync(capacity);
        }
        catch (Exception e)
        {
            LastError = $"방을 만들지 못했다: {e.Message}";
            return false;
        }

        if (!created.HasValue)
        {
            LastError = "방을 만들지 못했다.";
            return false;
        }

        var lobby = created.Value;
        lobby.MaxMembers = capacity;
        lobby.SetData(GameKey, GameValue);
        lobby.SetData(NameKey, roomName);
        lobby.SetData(HostKey, SelfId.ToString());
        lobby.SetData(TeamsKey, teamCount.ToString());
        lobby.SetPublic();
        lobby.SetJoinable(true);

        current = lobby;
        LastError = string.Empty;
        return true;
    }

    public async UniTask<bool> JoinRoomAsync(LobbyRoom room)
    {
        if (!Ready)
        {
            LastError = "스팀이 준비되지 않았다.";
            return false;
        }

        var index = handles.FindIndex(lobby => lobby.Id.Value == room.Id);
        if (index < 0)
        {
            LastError = "방 목록이 낡았다. 새로 고친 뒤 다시 시도한다.";
            return false;
        }

        RoomEnter entered;
        try
        {
            entered = await handles[index].Join();
        }
        catch (Exception e)
        {
            LastError = $"방에 들어가지 못했다: {e.Message}";
            return false;
        }

        if (entered != RoomEnter.Success)
        {
            LastError = $"방에 들어가지 못했다: {entered}";
            return false;
        }

        current = handles[index];
        LastError = string.Empty;
        return true;
    }

    public void LeaveRoom()
    {
        current?.Leave();
        current = null;
    }

    // --- 대기실 ---

    public bool InRoom => current.HasValue;
    public bool IsHost => current.HasValue && Ready && current.Value.IsOwnedBy(SteamClient.SteamId);
    public ulong RoomHostId => current.HasValue ? current.Value.Owner.Id : 0ul;
    public string RoomName => current.HasValue ? current.Value.GetData(NameKey) : string.Empty;

    public int RoomTeamCount =>
        current.HasValue && int.TryParse(current.Value.GetData(TeamsKey), out var count) ? count : 0;

    public IReadOnlyList<LobbyMember> ReadMembers()
    {
        members.Clear();
        if (!current.HasValue) return members;

        var lobby = current.Value;
        foreach (var member in lobby.Members)
            members.Add(new LobbyMember(member.Id, member.Name,
                                        LobbyMember.ParseTeam(lobby.GetMemberData(member, TeamKey)),
                                        lobby.GetMemberData(member, ReadyKey) == "1",
                                        LobbyMember.ParsePick(lobby.GetMemberData(member, PickKey))));
        return members;
    }

    public void WriteSelf(int team, bool ready, int character)
    {
        if (!current.HasValue) return;

        var lobby = current.Value;
        lobby.SetMemberData(TeamKey, team.ToString());
        lobby.SetMemberData(ReadyKey, ready ? "1" : "0");
        lobby.SetMemberData(PickKey, character.ToString());
    }

    // --- 시작 ---

    public void CloseRoom()
    {
        if (!current.HasValue) return;

        var lobby = current.Value;
        lobby.SetJoinable(false);
        lobby.SetData(LiveKey, GameValue);
    }

    /// 손님은 이것이 일으키는 `OnLobbyGameCreated`를 받고 붙는다 — 스팀이 이 용도로 준 경로다.
    public void ReopenRoom()
    {
        if (!current.HasValue || !IsHost) return;
        var lobby = current.Value;
        lobby.SetData(LiveKey, string.Empty);
        lobby.SetJoinable(true);
    }

    public void AnnounceServer()
    {
        if (Ready) current?.SetGameServer(SteamClient.SteamId);
    }
}
