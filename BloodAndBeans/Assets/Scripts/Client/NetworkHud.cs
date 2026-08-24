using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// Development connection controls, rendered with uGUI.
public class NetworkHud : MonoBehaviour
{
    Text status;
    Button host;
    Button client;
    Button server;
    Button shutdown;

    void Awake()
    {
        if (FindFirstObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var canvasObject = new GameObject("Network HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 20;

        var panel = new GameObject("Connection", typeof(RectTransform), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(canvasObject.transform, false);
        var rect = (RectTransform)panel.transform;
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(12f, -12f);
        rect.sizeDelta = new Vector2(180f, 180f);

        status = MakeText(panel.transform, "Disconnected");
        host = MakeButton(panel.transform, "Host", () => NetworkManager.Singleton?.StartHost());
        client = MakeButton(panel.transform, "Client", () => NetworkManager.Singleton?.StartClient());
        server = MakeButton(panel.transform, "Server", () => NetworkManager.Singleton?.StartServer());
        shutdown = MakeButton(panel.transform, "Shutdown", () => NetworkManager.Singleton?.Shutdown());
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

    static Text MakeText(Transform parent, string value)
    {
        var gameObject = new GameObject("Label", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        gameObject.transform.SetParent(parent, false);
        var text = gameObject.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = 16;
        text.color = Color.white;
        text.text = value;
        gameObject.GetComponent<LayoutElement>().preferredHeight = 28f;
        return text;
    }

    static Button MakeButton(Transform parent, string label, UnityEngine.Events.UnityAction action)
    {
        var gameObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        gameObject.transform.SetParent(parent, false);
        gameObject.GetComponent<Image>().color = new Color(0.18f, 0.20f, 0.24f, 0.95f);
        gameObject.GetComponent<LayoutElement>().preferredHeight = 32f;
        var button = gameObject.GetComponent<Button>();
        button.onClick.AddListener(action);
        var text = MakeText(gameObject.transform, label);
        text.alignment = TextAnchor.MiddleCenter;
        var rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return button;
    }
}
