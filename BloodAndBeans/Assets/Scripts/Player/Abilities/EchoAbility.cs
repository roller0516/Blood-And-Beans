/// 메아리 — 주변 안개를 즉시 걷는다 (기획서 9.2).
///
/// 걷힌 칸은 전원이 공유하므로(기획서 6.1-3) 남에게도 길을 열어 준다. 그것이 이 스킬의 값이다.
public sealed class EchoAbility : INightAbility
{
    public NightSkill Id => NightSkill.Echo;

    public bool TryCastServer(PlayerAbilities host)
    {
        var fog = host.Fog;
        if (fog == null) return false;

        var radius = NightSkills.EchoRadius;
        var at = host.transform.position;

        fog.RevealBurstServer(at, radius);
        host.CueServer(EffectId.Echo, at, radius);
        return true;
    }
}
