using Cysharp.Threading.Tasks;
using UnityEngine;

/// 지름길 — 잠시 다른 플레이어를 통과한다 (기획서 9.1.2).
///
/// **모든 피어가 같은 쌍의 충돌을 끈다.** 서버만 끄면 소유자 예측이 막혀 화해가 당긴다.
/// 그래서 지속 슬롯이 전원에게 복제되고, 이 클래스도 **모든 피어에 하나씩 산다**
/// (`PlayerAbilities.Rebuild`).
///
/// 끝은 `LocalTime`으로 잰다 — 클라이언트의 `ServerTime`은 RTT+버퍼만큼 늦어 서버보다
/// 한참 늦게 끝난다. `LocalTime`이면 오차가 반 RTT 안쪽으로 줄어든다. 서버에서는 같다.
public sealed class ShortcutAbility : IDayAbility, IDurationAbility
{
    public DaySkill Id => DaySkill.Shortcut;

    /// 붙는 연출이 없다. 통과는 눈에 보이는 것이 아니라 부딪히지 않는 것이다.
    public EffectId AttachedEffect => EffectId.None;

    public bool TryCastServer(PlayerAbilities host)
    {
        host.SetDurationServer(DaySkills.ShortcutSeconds);
        return true;
    }

    public void OnDurationChanged(PlayerAbilities host, double until)
    {
        var manager = host.NetworkManager;
        if (manager == null) return;

        var seconds = (float)(until - manager.LocalTime.Time);
        if (seconds > 0f) PassThroughAsync(host, seconds).Forget();
    }

    async UniTaskVoid PassThroughAsync(PlayerAbilities host, float seconds)
    {
        SetPassThrough(host, true);
        var cancelled = await UniTask.Delay(System.TimeSpan.FromSeconds(seconds),
            cancellationToken: host.GetCancellationTokenOnDestroy()).SuppressCancellationThrow();
        if (!cancelled) SetPassThrough(host, false);
    }

    /// 발동과 종료에 한 번씩 도는 순회다. 효과 도중 새로 스폰된 플레이어는 막힌다 (2.5초라 무시한다).
    static void SetPassThrough(PlayerAbilities host, bool ignore)
    {
        var controller = host.Controller;
        var manager = host.NetworkManager;
        if (controller == null || manager == null || manager.SpawnManager == null) return;

        foreach (var player in manager.SpawnManager.PlayerObjects)
        {
            if (player == null || player == host.NetworkObject ||
                !player.TryGetComponent<CharacterController>(out var other)) continue;

            // 둘이 겹쳤으면 늦게 끝나는 쪽이 쌍을 되돌린다. 시계가 아니라 종료 시각을 비교해야
            // 두 피어 시계 오차로 서로 미루다 영구히 통과로 남는 일이 없다.
            var keep = ignore ||
                (player.TryGetComponent<PlayerAbilities>(out var peer) && peer.DurationUntil > host.DurationUntil);
            Physics.IgnoreCollision(controller, other, keep);
        }
    }
}
