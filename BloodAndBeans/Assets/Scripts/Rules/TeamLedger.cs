/// 한 팀의 매치 내 상태: 얼마를 빚졌고 그 대가가 무엇인지.
///
/// 원래는 `static Dictionary<int, RentPenalty>`였고 플레이 세션보다 오래 살아남아 매치를
/// 시작할 때마다 손으로 비워야 했다 (아키텍처_v1.0.md §1.5). MatchDirector가 소유하는
/// 인스턴스로 바꾸면서 매치와 함께 사라지고, 페널티 표는 씬 없이 검증 가능한 규칙이 됐다.
///
/// 페널티는 매출을 직접 깎지 않는다 — 기획서 3.3이 벌은 마찰이지 수익의 몫이 아니라고
/// 못박았다.
///
/// 페널티는 보석(8장)과 **같은 축에 마이너스로** 붙는다 — 제작 속도는 「불씨」, 이동은
/// 「바람」의 반대 방향이다 (기획서 3.3).
public class TeamLedger
{
    /// 감소를 다 먹여도 남는 최솟값. 속도가 0이면 그 팀은 그날 아무것도 못 하고, 음수가 되면
    /// 시간이 거꾸로 간다. 단계별 감소율 표와 함께 `BalanceData`에 있다 (기획서 3.3).
    public static float MinScale => Balance.Current.PenaltyMinScale;

    public Rent Rent { get; } = new();

    public TeamGems Gems { get; } = new();

    /// 정산 시점에 적용되어 낮 하루와 이어지는 밤 동안만 유지된다 (기획서 3.3).
    /// 그 추적은 이미 `Rent.Penalty`가 한다. 별도 값으로 둔 이유는 낮 도중에 임대료를 내도
    /// 그날의 페널티가 플레이어 발밑에서 바뀌지 않게 하기 위해서다.
    public RentPenalty Penalty { get; private set; }

    public void ApplySettledPenalty() => Penalty = Rent.Penalty;

    // --- 낮 (기획서 3.3) ---

    /// 제작 시간에 곱하는 값이다. 속도 10% 감소는 시간 1/0.9배다.
    public float CraftTimeScale => 1f / SpeedScale(Balance.Current.PenaltyCraftLoss);

    /// 이동 속도에 곱하는 값. 낮 페널티라 밤에는 걸지 않는다.
    public float MoveSpeedScale => SpeedScale(Balance.Current.PenaltyMoveLoss);

    // --- 밤 (기획서 3.3) ---

    public float VisionScale => SpeedScale(Balance.Current.PenaltyVisionLoss);

    /// 개봉 시간에 곱하는 값. 개봉 속도 20% 감소는 시간 1/0.8배다.
    public float BoxOpenTimeScale => 1f / SpeedScale(Balance.Current.PenaltyOpenLoss);

    public bool WeightBandShifted => Penalty >= RentPenalty.Tier3;

    /// 감소율을 배수로 바꾼다. **0 이하로는 내려가지 않는다.**
    float SpeedScale(float[] table) =>
        System.Math.Max(MinScale, 1f - table[(int)Penalty]);
}
