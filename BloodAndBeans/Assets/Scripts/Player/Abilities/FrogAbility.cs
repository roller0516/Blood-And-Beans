/// 개구리 저주 — 맞은 적은 아무것도 집거나 쓸 수 없다 (기획서 9.1.2).
public sealed class FrogAbility : IDayAbility
{
    public DaySkill Id => DaySkill.FrogCurse;

    /// 던지는 스킬이라 빗나가도 쓴 것이다. 빗나가면 사거리 끝에서 연기만 터진다.
    public bool TryCastServer(PlayerAbilities host)
    {
        var range = DaySkills.FrogRange;
        var target = host.NearestEnemy(range, aheadOnly: true);
        if (target != null && target.FrogServer()) return true;

        host.TryGroundAhead(range, out var at);
        host.CueServer(EffectId.FrogCurse, at);
        return true;
    }
}
