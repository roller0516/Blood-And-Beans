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

        // ponytail: 가방 목록을 들고 있는 곳이 없어 전수 조사로 둔다. 상자처럼
        // `MatchDirector`가 모으게 되면 여기도 그 목록을 읽는다.
        foreach (var bag in Object.FindObjectsByType<BuriedBag>())
        {
            if (bag == null || !bag.NetworkObject.IsSpawned) continue;
            if (Vector3.Distance(bag.transform.position, at) > radius) continue;

            bag.RevealToServer(host.OwnerClientId, revealSeconds);

            // 냄새 줄기는 본인에게만 보인다 (기획서 9.2).
            host.CueToOwnerServer(EffectId.TrackScent, bag.transform.position);
        }

        // 남에게는 코 아이콘과 「킁킁」만 보인다 (9.2). 냄새를 맡는 것 자체가 스킬이라
        // 가방이 없어도 쓴 것이다 — 「주변에 없다」도 정보다.
        host.CueServer(EffectId.TrackSniff, at);
        return true;
    }
}
