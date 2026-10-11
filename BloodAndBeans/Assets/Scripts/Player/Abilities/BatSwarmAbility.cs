/// 박쥐 떼 — 맞은 적의 시야가 코앞으로 좁아진다 (기획서 9.2).
public sealed class BatSwarmAbility : INightAbility
{
    public NightSkill Id => NightSkill.BatSwarm;

    /// 던지는 스킬이라 빗나가도 쓴 것이다. 빗나가면 사거리 끝에서 박쥐가 흩어진다.
    public bool TryCastServer(PlayerAbilities host)
    {
        var range = NightSkills.BatSwarmRange;
        var target = host.NearestEnemy(range, aheadOnly: true);
        if (target != null && target.BlindServer()) return true;

        host.TryGroundAhead(range, out var at);
        host.CueServer(EffectId.BatSwarm, at, 1f, NightSkills.BatSwarmSeconds);
        return true;
    }
}
