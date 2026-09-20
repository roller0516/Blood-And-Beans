/// 삼키기 — 들고 있는 더러운 식기의 세척을 미리 진행시킨다 (기획서 9.1.2).
///
/// 1회 1개다. 이미 그만큼 진행된 식기에는 걸지 않는다 — 걸리면 쿨타임만 태우고 아무 일도
/// 일어나지 않는다.
public sealed class SwallowAbility : IDayAbility
{
    public DaySkill Id => DaySkill.Swallow;

    public bool TryCastServer(PlayerAbilities host)
    {
        var carry = host.Carry;
        if (carry == null || carry.Reserved || !carry.Held.Dirty ||
            carry.Held.WashProgress >= DaySkills.SwallowProgress) return false;

        var item = carry.Held;
        item.WashProgress = DaySkills.SwallowProgress;
        carry.SetServer(item);
        return true;
    }
}
