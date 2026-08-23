using System;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

/// Starts the main editor as Host and Multiplayer Play Mode virtual players as Client,
/// so a 2-player session comes up without clicking anything.
/// ponytail: reflection because UnityEngine.MultiplayerModule isn't referenced by Assembly-CSharp;
/// swap to a direct reference if an asmdef ever pulls the module in.
public class NetworkAutoStart : MonoBehaviour
{
    [SerializeField] bool enableAutoStart = true;

    void Start()
    {
        if (!enableAutoStart) return;

        var nm = NetworkManager.Singleton;
        if (nm == null || nm.IsListening) return;

        if (IsMainEditor()) nm.StartHost();
        else nm.StartClient();
    }

    static bool IsMainEditor()
    {
        var t = Type.GetType("Unity.Multiplayer.PlayMode.CurrentPlayer, UnityEngine.MultiplayerModule");
        var p = t?.GetProperty("IsMainEditor", BindingFlags.Public | BindingFlags.Static);
        if (p == null) return true; // MPPM absent — behave like a normal single editor
        return (bool)p.GetValue(null);
    }
}
