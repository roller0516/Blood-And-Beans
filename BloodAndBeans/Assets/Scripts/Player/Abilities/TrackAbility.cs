using UnityEngine;

/// 추적 — 주변에 묻힌 가방을 잠시 드러낸다 (기획서 9.2).
///
/// 적이 묻은 것도 보인다 — 그것이 목적이다 (기획서 6.7 「적 가방 탐색 및 파괴」).
public sealed class TrackAbility : INightAbility
{
    public NightSkill Id => NightSkill.Track;

    public bool TryCastServer(PlayerAbilities host)
    {
        var radius = NightSkills.TrackRadius;
        var revealSeconds = NightSkills.TrackRevealSeconds;
        var at = host.transform.position;
        var hit = false;

        // ponytail: 가방 목록을 들고 있는 곳이 없어 전수 조사로 둔다. 상자처럼
        // `MatchDirector`가 모으게 되면 여기도 그 목록을 읽는다.
        foreach (var bag in Object.FindObjectsByType<BuriedBag>(FindObjectsSortMode.None))
        {
            if (bag == null || !bag.NetworkObject.IsSpawned) continue;
            if (Vector3.Distance(bag.transform.position, at) > radius) continue;

            bag.RevealToServer(host.OwnerClientId, revealSeconds);
            hit = true;
        }
        return hit;
    }
}
