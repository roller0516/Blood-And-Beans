using System;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

/// 메인 에디터는 Host로, Multiplayer Play Mode 가상 플레이어는 Client로 시작시킨다.
/// 클릭 없이 2인 세션이 뜨게 하기 위한 것이다.
/// ponytail: Assembly-CSharp가 UnityEngine.MultiplayerModule을 참조하지 않아 리플렉션을 쓴다.
/// asmdef가 그 모듈을 끌어오게 되면 직접 참조로 바꾼다.
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
        if (p == null) return true; // MPPM이 없다 — 평범한 단일 에디터처럼 동작한다
        return (bool)p.GetValue(null);
    }
}
