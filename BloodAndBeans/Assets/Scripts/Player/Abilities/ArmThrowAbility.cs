/// 팔 던지기 — 맞은 적은 움직일 수 없고 다른 행동은 된다 (기획서 9.2).
public sealed class ArmThrowAbility : INightAbility
{
    public NightSkill Id => NightSkill.ArmThrow;

    /// 던지는 스킬이라 빗나가도 쓴 것이다. 빗나가면 사거리 끝에서 팔이 바닥을 긁는다.
    public bool TryCastServer(PlayerAbilities host)
    {
        var range = NightSkills.ArmThrowRange;
        var target = host.NearestEnemy(range, aheadOnly: true);
        if (target != null && target.RootServer()) return true;

        host.TryGroundAhead(range, out var at);
        host.CueServer(EffectId.ArmGrab, at, 1f, NightSkills.ArmThrowSeconds);
        return true;
    }
}
