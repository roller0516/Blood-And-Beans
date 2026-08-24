/// Miss-penalty tier (design doc 3.3). Lasts exactly one day + the following
/// night, and clears the moment rent is fully paid. Never touches revenue.
public enum RentPenalty { None, Tier1, Tier2, Tier3 }

/// Daily rent, debt carry-over and miss penalties (design doc 3.2 / 3.3).
/// One instance per team, server-side only.
public class Rent
{
    // ponytail: hard-coded table, doc 3.2. Move to DT_Rent when days become data.
    static readonly int[] Table = { 60, 100, 160, 250, 380, 560, 800 };

    /// Days past the table hold at the last rent — the doc has no row beyond day 7 (3.2).
    /// System.Math rather than Mathf: BB.Rules does not reference UnityEngine.
    public static int Due(int day) =>
        Table[System.Math.Min(System.Math.Max(day, 1), Table.Length) - 1];

    public int Debt { get; private set; }
    public int MissStreak { get; private set; }

    /// Tier caps at 3 — doc lists no 4회 row, so a longer streak stays at Tier3.
    public RentPenalty Penalty => MissStreak switch
    {
        0 => RentPenalty.None,
        1 => RentPenalty.Tier1,
        2 => RentPenalty.Tier2,
        _ => RentPenalty.Tier3,
    };

    /// End-of-day settlement. Returns how much was actually paid.
    /// No interest on carried debt (3.2).
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
