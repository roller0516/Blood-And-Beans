using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// 미끄러운 물약 — 던진 자리의 웅덩이에 들어온 적이 넘어진다 (기획서 9.2).
///
/// 웅덩이는 물체가 아니라 서버의 판정 구역이다. 막는 것이 없어 소유자 예측이 알 필요가 없다.
public sealed class PotionAbility : INightAbility
{
    public NightSkill Id => NightSkill.SlipperyPotion;

    public bool TryCastServer(PlayerAbilities host)
    {
        if (!host.TryGroundAhead(NightSkills.PotionRange, out var at)) return false;

        // 대상은 던진 순간의 적 전원이다. 매 프레임 컴포넌트를 찾지 않게 여기서 한 번 모은다.
        var enemies = new List<PlayerStatus>();
        foreach (var client in host.NetworkManager.ConnectedClientsList)
        {
            if (client.PlayerObject == null || PlayerTeam.Of(client.ClientId) == host.TeamId) continue;
            var status = client.PlayerObject.GetComponent<PlayerStatus>();
            if (status != null) enemies.Add(status);
        }

        PuddleAsync(host, at, enemies).Forget();
        host.CueServer(EffectId.PotionPuddle, at, 1f, NightSkills.PotionSeconds);
        return true;
    }

    /// 넘어진 뒤 면역은 `PlayerStatus.FallServer`가 보므로 여기서는 반경만 잰다.
    static async UniTaskVoid PuddleAsync(PlayerAbilities host, Vector3 at, List<PlayerStatus> enemies)
    {
        var ct = host.GetCancellationTokenOnDestroy();
        var end = Time.time + NightSkills.PotionSeconds;
        var radius = NightSkills.PotionRadius;
        while (Time.time < end)
        {
            foreach (var status in enemies)
            {
                if (status == null) continue;
                var to = status.transform.position - at;
                to.y = 0f;
                if (to.magnitude <= radius) status.FallServer();
            }
            if (await UniTask.Yield(PlayerLoopTiming.Update, ct).SuppressCancellationThrow()) return;
        }
    }
}
