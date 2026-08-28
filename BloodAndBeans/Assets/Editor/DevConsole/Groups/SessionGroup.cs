using Unity.Netcode;
using UnityEngine.UIElements;

/// 접속 조작. Host/Client/Server로 뜨고 끊는다.
public class SessionGroup : DevConsoleGroup
{
    public override string Tab => "접속";
    public override string Title => "세션";

    Button host, client, server, shutdown;
    Label info;

    protected override void Build(VisualElement group)
    {
        var top = ButtonRow(group);
        host = Btn(top, "Host", StartHost, "btn--primary");
        client = Btn(top, "Client", StartClient);
        server = Btn(top, "Server", StartServer);

        shutdown = Btn(ButtonRow(group), "Shutdown", Shutdown, "btn--danger");

        info = Row(group, "상태", "연결 안 됨");
    }

    public override void Refresh(in DevConsoleState state)
    {
        // 재생 전에는 NetworkManager 자체가 없다. 누를 수 있게 두면 아무 일도 안 일어나
        // 고장으로 읽힌다.
        var canStart = state.Playing && !state.Listening;
        host.SetEnabled(canStart);
        client.SetEnabled(canStart);
        server.SetEnabled(canStart);
        shutdown.SetEnabled(state.Listening);

        var manager = NetworkManager.Singleton;
        info.text = !state.Listening ? "연결 안 됨"
            : $"{(state.IsServer ? manager.ConnectedClients.Count : 1)}명 접속";
    }

    static void StartHost() { var n = NetworkManager.Singleton; if (n != null) n.StartHost(); }

    static void StartClient() { var n = NetworkManager.Singleton; if (n != null) n.StartClient(); }

    static void StartServer() { var n = NetworkManager.Singleton; if (n != null) n.StartServer(); }

    static void Shutdown() { var n = NetworkManager.Singleton; if (n != null) n.Shutdown(); }
}
