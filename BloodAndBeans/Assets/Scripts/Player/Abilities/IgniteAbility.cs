/// 불붙이기 — 내가 조리 중인 설비의 남은 시간을 깎는다 (기획서 9.1.2).
///
/// **설비를 쓰고 있지 않으면 발동하지 않는다.** 먼저 점유해야 걸리는 스킬이라
/// (`Station.IgniteServer`) 빈손으로 누르면 쿨타임도 돌지 않는다.
public sealed class IgniteAbility : IDayAbility
{
    public DaySkill Id => DaySkill.Ignite;

    public bool TryCastServer(PlayerAbilities host)
    {
        // 광장 게이지 전부를 본다. 남의 설비는 Station이 조리자 id로 거른다.
        var director = MatchDirector.Instance;
        if (director == null) return false;

        foreach (var gauge in director.PlazaGauges)
        {
            if (gauge == null || gauge.Station == null || !gauge.Station.IgniteServer(host.OwnerClientId)) continue;

            host.CueServer(EffectId.Ignite, gauge.Station.FacilityPosition);
            return true;
        }
        return false;
    }
}
