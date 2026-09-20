/// 임대료 미납 페널티 단계 (기획서 3.3). 정확히 낮 하루 + 이어지는 밤 동안 유지되고,
/// 임대료를 완납하는 즉시 풀린다. 매출에는 절대 관여하지 않는다.
public enum RentPenalty { None, Tier1, Tier2, Tier3 }

/// 일일 임대료, 미납금 이월, 미납 페널티 (기획서 3.2 / 3.3).
/// 팀당 인스턴스 하나, 서버 전용이다.
public class Rent
{
    /// 표를 넘는 일차는 마지막 임대료로 고정한다. 기획서에 8일차 이후 행이 없다 (3.2).
    /// 표는 `BalanceData.RentByDay`가 갖는다 — 데이터 파일이 덮어쓸 수 있는 값이다.
    public static int Due(int day) => Balance.ByDay(Balance.Current.RentByDay, day);

    public int Debt { get; private set; }
    public int MissStreak { get; private set; }

    /// 단계는 3에서 멈춘다. 기획서에 4회 행이 없으므로 그 이상 연속되어도 Tier3을 유지한다.
    public RentPenalty Penalty => MissStreak switch
    {
        0 => RentPenalty.None,
        1 => RentPenalty.Tier1,
        2 => RentPenalty.Tier2,
        _ => RentPenalty.Tier3,
    };

    /// 낮 종료 정산. 실제로 낸 금액을 돌려준다.
    /// 이월된 미납금에 이자는 붙지 않는다 (3.2).
    public int Settle(int day, int cash)
    {
        var owed = Due(day) + Debt;
        var paid = cash < owed ? cash : owed;
        if (paid < 0) paid = 0;

        Debt = owed - paid;
        MissStreak = Debt > 0 ? MissStreak + 1 : 0;
        return paid;
    }
}
