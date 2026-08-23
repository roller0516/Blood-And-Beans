using Unity.Netcode;
using UnityEngine;

/// Minimal Host/Client/Server entry point for local testing.
/// ponytail: OnGUI instead of a uGUI canvas — replace when the real lobby UI lands.
public class NetworkHud : MonoBehaviour
{
    void OnGUI()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        GUILayout.BeginArea(new Rect(10, 10, 200, 120));

        if (!nm.IsClient && !nm.IsServer)
        {
            if (GUILayout.Button("Host")) nm.StartHost();
            if (GUILayout.Button("Client")) nm.StartClient();
            if (GUILayout.Button("Server")) nm.StartServer();
        }
        else
        {
            GUILayout.Label($"Mode: {(nm.IsHost ? "Host" : nm.IsServer ? "Server" : "Client")}");
            GUILayout.Label($"Clients: {(nm.IsServer ? nm.ConnectedClients.Count : 1)}");
            if (GUILayout.Button("Shutdown")) nm.Shutdown();
        }

        GUILayout.EndArea();
    }
}
