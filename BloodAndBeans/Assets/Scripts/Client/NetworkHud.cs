using Unity.Netcode;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 개발용 접속 조작 UI. uGUI로 그린다.
/// 좌상단 개발 열의 첫 번째 패널이다. 아래에 `CheatHud`가 붙으므로 크기를 바꾸면
/// 그쪽 `anchoredPosition`도 같이 옮겨야 한다.
public class NetworkHud : MonoBehaviour
{
    [Header("레이아웃")]
    [SerializeField] Vector2 anchoredPosition = new(12f, -12f);
    [SerializeField] Vector2 size = new(180f, 180f);
    [SerializeField] int sortingOrder = 20;

    TMP_Text status;
    Button host;
    Button client;
    Button server;
    Button shutdown;

    void Awake()
    {
        DevHud.EnsureEventSystem();
        var panel = DevHud.MakePanel(transform, "Network HUD", sortingOrder, anchoredPosition, size);

        status = DevHud.MakeText(panel, "Disconnected");
        host = DevHud.MakeButton(panel, "Host", () => NetworkManager.Singleton?.StartHost());
        client = DevHud.MakeButton(panel, "Client", () => NetworkManager.Singleton?.StartClient());
        server = DevHud.MakeButton(panel, "Server", () => NetworkManager.Singleton?.StartServer());
        shutdown = DevHud.MakeButton(panel, "Shutdown", () => NetworkManager.Singleton?.Shutdown());
    }

    void Update()
    {
        var manager = NetworkManager.Singleton;
        if (manager == null) return;
        var connected = manager.IsClient || manager.IsServer;
        host.gameObject.SetActive(!connected);
        client.gameObject.SetActive(!connected);
        server.gameObject.SetActive(!connected);
        shutdown.gameObject.SetActive(connected);
        status.text = !connected ? "Disconnected" :
            $"{(manager.IsHost ? "Host" : manager.IsServer ? "Server" : "Client")} · " +
            $"{(manager.IsServer ? manager.ConnectedClients.Count : 1)} client(s)";
    }
}
