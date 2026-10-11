using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

/// 관 떨어뜨리기가 세우는 벽 (기획서 9.1.2). 정해진 시간 뒤 스스로 사라진다.
///
/// 프리팹은 바닥 크기 1×1m로 만들고, 실제 크기(가로 × 세로)는 스폰 전에 표 값으로 늘린다.
/// 막는 것이 요점이라 소유자 예측도 같은 벽에 막혀야 한다 — 그래서 네트워크 오브젝트다.
public class Coffin : NetworkBehaviour
{
    public override void OnNetworkSpawn()
    {
        if (IsServer) DespawnLaterAsync().Forget();
    }

    async UniTaskVoid DespawnLaterAsync()
    {
        var cancelled = await UniTask.Delay(System.TimeSpan.FromSeconds(DaySkills.CoffinSeconds),
            cancellationToken: this.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
        if (!cancelled && IsSpawned) NetworkObject.Despawn();
    }

    /// 관이 떨어진 자리에 서 있던 사람은 옆으로 밀려난다 (9.1.2). 관의 짧은 축으로 민다.
    public void ShoveOccupantsServer()
    {
        if (!IsServer) return;
        var half = new Vector2(DaySkills.CoffinWidth, DaySkills.CoffinDepth) * 0.5f;
        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            var player = client.PlayerObject;
            if (player == null) continue;
            var controller = player.GetComponent<CharacterController>();
            var radius = controller != null ? controller.radius : 0f;
            var local = transform.InverseTransformPoint(player.transform.position);
            // 로컬 좌표는 늘린 크기로 나뉘어 있다. 미터로 되돌려 잰다.
            var x = local.x * transform.lossyScale.x;
            var z = local.z * transform.lossyScale.z;
            if (Mathf.Abs(x) > half.x + radius || Mathf.Abs(z) > half.y + radius) continue;

            var side = z >= 0f ? 1f : -1f;
            var push = transform.forward * (side * (half.y + radius) - z);
            player.GetComponent<DashHarass>()?.PushServer(push, 0f, 0.1f, topple: false);
        }
    }
}
