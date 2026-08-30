using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// 플레이어가 지나가면 그 자리 풀이 눕는다. 위치만 셰이더에 넘기고, 눕히는 계산은
/// 블레이드 셰이더가 버텍스에서 한다 (`BloodAndBeans/ForestGrassBlade`).
///
/// 풀은 컴퓨트가 위치를 뽑아 인디렉트 드로우 한 번으로 그린다(`ForestGrass`). 잎 하나하나를
/// CPU가 아는 구조가 아니고, 알 필요도 없다 — 매 프레임 오가는 것은 float4 네 칸뿐이다.
///
/// 명단은 이벤트가 있을 때만 다시 만든다. 매 프레임 플레이어를 찾으면 그것이 바로
/// 주기 실행 안의 탐색이다 (AGENTS.md 참조와 결합도).
public class GrassTrample : MonoBehaviour
{
    /// 셰이더의 `GRASS_TRAMPLERS`와 같아야 한다. 남는 칸은 w=0으로 꺼 둔다.
    const int MaxTramplers = 4;

    static readonly int Tramplers = Shader.PropertyToID("_GrassTramplers");

    readonly List<Transform> players = new();
    readonly Vector4[] payload = new Vector4[MaxTramplers];

    NetworkManager subscribed;

    void OnEnable()
    {
        Rescan();

        subscribed = NetworkManager.Singleton;
        if (subscribed != null) subscribed.OnConnectionEvent += OnConnectionEvent;
    }

    void OnDisable()
    {
        if (subscribed != null) subscribed.OnConnectionEvent -= OnConnectionEvent;
        subscribed = null;

        // 꺼질 때 자국을 남기지 않는다. 전역 값이라 아무도 지워 주지 않는다.
        for (var i = 0; i < payload.Length; i++) payload[i] = Vector4.zero;
        Shader.SetGlobalVectorArray(Tramplers, payload);
    }

    void OnConnectionEvent(NetworkManager _, ConnectionEventData __) => Rescan();

    /// 접속·해제처럼 명단이 바뀔 때만 돈다.
    void Rescan()
    {
        players.Clear();

        var manager = NetworkManager.Singleton;
        var spawner = manager != null && manager.IsListening ? manager.SpawnManager : null;
        if (spawner == null) return;

        foreach (var pair in spawner.SpawnedObjects)
        {
            var networkObject = pair.Value;
            if (networkObject == null || !networkObject.IsPlayerObject) continue;
            if (players.Count >= MaxTramplers) break;

            players.Add(networkObject.transform);
        }
    }

    /// 매 프레임 하는 일은 자리 네 개를 셰이더에 넣는 것뿐이다. 탐색도 할당도 없다.
    void LateUpdate()
    {
        var used = 0;
        for (var i = 0; i < players.Count && used < MaxTramplers; i++)
        {
            var player = players[i];
            if (player == null) continue;     // 스폰 해제된 자리는 다음 이벤트에서 정리된다

            var position = player.position;
            payload[used++] = new Vector4(position.x, position.z, 0f, 1f);
        }

        for (var i = used; i < payload.Length; i++) payload[i] = Vector4.zero;
        Shader.SetGlobalVectorArray(Tramplers, payload);
    }
}
