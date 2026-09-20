/// 정제 — 다음 한 잔을 Perfect로 확정한다 (기획서 9.1.2).
///
/// 발동과 소모가 갈리는 유일한 능력이다. 쓰는 쪽은 완성 게이지고(`Station.OnJudged`),
/// 장전 상태는 라우터의 플래그 슬롯이 든다.
public sealed class RefineAbility : IDayAbility
{
    public DaySkill Id => DaySkill.Refine;

    public bool TryCastServer(PlayerAbilities host)
    {
        if (host.Flag) return false;        // 1회성이라 겹쳐 쌓지 않는다
        host.SetFlagServer(true);
        return true;
    }

    /// 장전돼 있었으면 소모하고 true. 게이지가 판정하는 순간에 부른다.
    public bool ConsumeServer(PlayerAbilities host)
    {
        if (!host.IsServer || !host.Flag) return false;
        host.SetFlagServer(false);
        return true;
    }
}
