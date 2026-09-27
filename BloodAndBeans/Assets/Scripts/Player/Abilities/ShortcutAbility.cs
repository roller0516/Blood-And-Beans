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

    /// 통과는 물리로만 나타난다. 이동 속도는 건드리지 않는다.
    public float MoveSpeedScale => 1f;

    public bool TryCastServer(PlayerAbilities host)
    {
        host.SetDurationServer(DaySkills.ShortcutSeconds);
        return true;
    }

    /// **모든 지속 변화(발동·연장·재발동·페이즈 리셋·캐릭터 교체·디스폰)가 여기로 온다.**
    /// `until`이 지금 이하면 "지금 끝났다"는 뜻이라 동기로 바로 되돌린다 — 재발동으로
    /// 이전 대기가 취소되면 그 대기는 `host.DurationToken`이 끊겨 자기 몫의 되돌리기를
    /// 하지 않으므로, 끝나는 경로는 여기서 반드시 한 번 정리해야 한다.
    public void OnDurationChanged(PlayerAbilities host, double until)
    {
        var manager = host.NetworkManager;
        if (manager == null) return;

        var seconds = (float)(until - manager.LocalTime.Time);
        if (seconds > 0f) PassThroughAsync(host, seconds).Forget();
        else SetPassThrough(host, false);
    }

    async UniTaskVoid PassThroughAsync(PlayerAbilities host, float seconds)
    {
        SetPassThrough(host, true);

        // 이 토큰은 다음 지속 변화(재발동·페이즈 리셋·캐릭터 교체·디스폰)마다 새로 갈린다
        // (`PlayerAbilities.RenewDurationToken`). 취소되면 이 대기는 자기 몫의 되돌리기를
        // 하지 않는다 — 새 변화 쪽 `OnDurationChanged` 호출이 이미 맞는 상태를 정한다.
        var cancelled = await UniTask.Delay(System.TimeSpan.FromSeconds(seconds),
            cancellationToken: host.DurationToken).SuppressCancellationThrow();
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

            // 둘이 겹쳤으면 상대가 지금도 자기 지름길을 쓰는 중이면 쌍을 되돌리지 않는다.
            // 능력 종류(지름길인가)와 활성 상태(남은 시간이 있는가)를 함께 봐야 한다 —
            // 상대가 다른 지속 능력(활공 등)으로 `DurationUntil`이 늦게 끝나는 것만으로는
            // 통과를 유지할 이유가 안 된다.
            var keep = ignore || (player.TryGetComponent<PlayerAbilities>(out var peer) &&
                peer.Ability<ShortcutAbility>() != null && peer.DurationRemaining > 0f);
            Physics.IgnoreCollision(controller, other, keep);
        }
    }
}
