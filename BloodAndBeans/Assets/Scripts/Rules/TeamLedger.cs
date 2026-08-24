/// One team's standing in the match: what it owes and what that costs it.
///
/// This was a `static Dictionary<int, RentPenalty>` that outlived play sessions and had
/// to be cleared by hand at every match start (아키텍처_v1.0.md §1.5). As an instance
/// owned by MatchDirector it dies with the match, and the penalty table becomes a rule
/// that can be checked without a scene.
///
/// 페널티는 매출을 직접 깎지 않는다 — 기획서 3.3이 벌은 마찰이지 수익의 몫이 아니라고
/// 못박았다.
///
/// | 연속 미납 | 낮                                | 밤                          |
/// |----------|-----------------------------------|-----------------------------|
/// | 1회      | 제작 속도 10% 감소                 | 시야 반경 감소               |
/// | 2회      | 커피 머신 1대 불통                 | + 박스 개봉 속도 감소        |
/// | 3회      | 머신 1대 불통 + 그릇 1개 파손       | + 무게 감속 한 단계 불리     |
public class TeamLedger
{
    public Rent Rent { get; } = new();

    /// Applied at settlement and held for exactly one day and the following night
    /// (doc 3.3), which is what `Rent.Penalty` already tracks. Kept as a separate value
    /// so the day's penalties do not shift under the players mid-day when rent is paid.
    public RentPenalty Penalty { get; private set; }

    public void ApplySettledPenalty() => Penalty = Rent.Penalty;

    // --- 낮 (doc 3.3) ---

    /// Cook times are multiplied, so a 10% speed loss is a 1.1x longer cook.
    public float CraftSpeedScale => Penalty == RentPenalty.None ? 1f : 1.10f;
    public bool MachineDown => Penalty >= RentPenalty.Tier2;
    public bool BreaksDish => Penalty >= RentPenalty.Tier3;

    // --- 밤 (doc 3.3) ---

    public float VisionScale => Penalty == RentPenalty.None ? 1f : 0.7f;
    public float BoxOpenScale => Penalty >= RentPenalty.Tier2 ? 1.5f : 1f;
    public bool WeightBandShifted => Penalty >= RentPenalty.Tier3;
}
