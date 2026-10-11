/// 물기 — 붙어 있는 적을 좀비걸음으로 만든다 (기획서 9.1.2). 옮기는 것은 `PlayerStatus`가 한다.
public sealed class BiteAbility : IDayAbility
{
    public DaySkill Id => DaySkill.Bite;

    public bool TryCastServer(PlayerAbilities host)
    {
        var target = host.NearestEnemy(DaySkills.BiteRange, aheadOnly: false);
        return target != null && target.BiteServer();
    }
}
