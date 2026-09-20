using UnityEngine;

/// 감별 — 근처 상자의 가려진 칸을 즉시 공개한다 (기획서 9.2).
///
/// 공개 상태는 상자마다 공유되므로(기획서 6.5.3) 뒤에 오는 사람도 그대로 본다.
public sealed class AppraiseAbility : INightAbility
{
    public NightSkill Id => NightSkill.Appraise;

    public bool TryCastServer(PlayerAbilities host)
    {
        var director = MatchDirector.Instance;
        if (director == null) return false;

        var radius = NightSkills.AppraiseRadius;
        var at = host.transform.position;
        var hit = false;

        foreach (var box in director.Boxes)
        {
            if (box == null || !box.NetworkObject.IsSpawned) continue;
            if (Vector3.Distance(box.transform.position, at) > radius) continue;

            box.RevealAllServer();
            hit = true;
        }
        return hit;
    }
}
