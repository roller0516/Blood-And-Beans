/// 한 팀의 매치 내 상태: 얼마를 빚졌고 그 대가가 무엇인지.
///
/// 원래는 `static Dictionary<int, RentPenalty>`였고 플레이 세션보다 오래 살아남아 매치를
/// 시작할 때마다 손으로 비워야 했다 (아키텍처_v1.0.md §1.5). MatchDirector가 소유하는
/// 인스턴스로 바꾸면서 매치와 함께 사라지고, 페널티 표는 씬 없이 검증 가능한 규칙이 됐다.
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

    /// 이 판에 설치한 설비 업그레이드 (기획서 8장). 효과가 "그 판 동안 영구"라서 수명이
    /// 임대료 장부와 같다 — 판이 끝나면 원장과 함께 사라지고 다음 판으로 넘어가지 않는다.
    public TeamUpgrades Upgrades { get; } = new();

    /// 정산 시점에 적용되어 낮 하루와 이어지는 밤 동안만 유지된다 (기획서 3.3).
    /// 그 추적은 이미 `Rent.Penalty`가 한다. 별도 값으로 둔 이유는 낮 도중에 임대료를 내도
    /// 그날의 페널티가 플레이어 발밑에서 바뀌지 않게 하기 위해서다.
    public RentPenalty Penalty { get; private set; }

    public void ApplySettledPenalty() => Penalty = Rent.Penalty;

    // --- 낮 (기획서 3.3) ---

    /// 제작 시간에 곱하는 값이므로 속도 10% 감소는 제작 시간 1.1배다.
    public float CraftSpeedScale => Penalty == RentPenalty.None ? 1f : 1.10f;
    public bool MachineDown => Penalty >= RentPenalty.Tier2;
    public bool BreaksDish => Penalty >= RentPenalty.Tier3;

    // --- 밤 (기획서 3.3) ---

    public float VisionScale => Penalty == RentPenalty.None ? 1f : 0.7f;
    public float BoxOpenScale => Penalty >= RentPenalty.Tier2 ? 1.5f : 1f;
    public bool WeightBandShifted => Penalty >= RentPenalty.Tier3;
}
