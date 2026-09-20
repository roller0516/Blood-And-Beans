using NUnit.Framework;

/// 기획서 3.3 미납 페널티 표. 이 표는 `static Dictionary<int, RentPenalty>`에 있어서 씬 없이는
/// 확인할 수 없었다 — 팀별 인스턴스가 되면서 표 자체를 문서와 대조할 수 있게 됐다.
public class TeamLedgerTests
{
    static TeamLedger AtStreak(int misses)
    {
        var ledger = new TeamLedger();
        for (var day = 1; day <= misses; day++) ledger.Rent.Settle(day, 0);
        ledger.ApplySettledPenalty();
        return ledger;
    }

    [Test]
    public void AFreshLedgerOwesNothingAndIsUnpunished()
    {
        var ledger = new TeamLedger();
        Assert.AreEqual(0, ledger.Rent.Debt);
        Assert.AreEqual(RentPenalty.None, ledger.Penalty);
        Assert.AreEqual(1f, ledger.CraftTimeScale, 0.0001f);
        Assert.AreEqual(1f, ledger.MoveSpeedScale, 0.0001f);
        Assert.AreEqual(1f, ledger.VisionScale, 0.0001f);
        Assert.AreEqual(1f, ledger.BoxOpenTimeScale, 0.0001f);
        Assert.IsFalse(ledger.WeightBandShifted);
    }

    [Test]
    public void FirstMissSlowsCraftingAndShrinksVisionOnly()
    {
        var ledger = AtStreak(1);
        Assert.AreEqual(RentPenalty.Tier1, ledger.Penalty);

        // 낮: 제작 속도 -10% = 조리 시간 1/0.9배. 이동은 2회부터다 (기획서 3.3).
        Assert.AreEqual(0.9f, 1f / ledger.CraftTimeScale, 0.0001f);
        Assert.AreEqual(1f, ledger.MoveSpeedScale, 0.0001f);

        // 밤: 시야 -15%만 걸리고 개봉 속도와 무게는 아직 멀쩡하다.
        Assert.AreEqual(0.85f, ledger.VisionScale, 0.0001f);
        Assert.AreEqual(1f, ledger.BoxOpenTimeScale, 0.0001f);
        Assert.IsFalse(ledger.WeightBandShifted);
    }

    [Test]
    public void SecondMissAddsMovementAndSlowerOpening()
    {
        // 기획서 3.3 2회 연속: 제작 -15% + 이동 -10% / 시야 -25% + 개봉 -20%.
        var ledger = AtStreak(2);
        Assert.AreEqual(RentPenalty.Tier2, ledger.Penalty);
        Assert.AreEqual(0.85f, 1f / ledger.CraftTimeScale, 0.0001f);
        Assert.AreEqual(0.9f, ledger.MoveSpeedScale, 0.0001f);
        Assert.AreEqual(0.75f, ledger.VisionScale, 0.0001f);
        Assert.AreEqual(0.8f, 1f / ledger.BoxOpenTimeScale, 0.0001f);
        Assert.IsFalse(ledger.WeightBandShifted);
    }

    [Test]
    public void ThirdMissHitsEveryAxisAndTheWeightBand()
    {
        // 기획서 3.3 3회 연속: 제작 -20% + 이동 -20% / 시야 -35% + 개봉 -30% + 무게 한 단계.
        var ledger = AtStreak(3);
        Assert.AreEqual(RentPenalty.Tier3, ledger.Penalty);
        Assert.AreEqual(0.8f, 1f / ledger.CraftTimeScale, 0.0001f);
        Assert.AreEqual(0.8f, ledger.MoveSpeedScale, 0.0001f);
        Assert.AreEqual(0.65f, ledger.VisionScale, 0.0001f);
        Assert.AreEqual(0.7f, 1f / ledger.BoxOpenTimeScale, 0.0001f);
        Assert.IsTrue(ledger.WeightBandShifted);
    }

    [Test]
    public void NoPenaltyEverReachesZeroOrGoesNegative()
    {
        // 페널티는 마찰이지 정지가 아니다. 어느 축도 0 이하로 내려가지 않는다.
        var ledger = AtStreak(9);   // 4회 이상은 3회와 같다 (기획서 3.2)
        Assert.AreEqual(RentPenalty.Tier3, ledger.Penalty);
        foreach (var scale in new[] { ledger.MoveSpeedScale, ledger.VisionScale,
                                      1f / ledger.CraftTimeScale, 1f / ledger.BoxOpenTimeScale })
        {
            Assert.GreaterOrEqual(scale, TeamLedger.MinScale);
            Assert.Greater(scale, 0f);
        }
        Assert.Greater(ledger.CraftTimeScale, 0f);
        Assert.Greater(ledger.BoxOpenTimeScale, 0f);
    }

    [Test]
    public void PenaltiesNeverTouchRevenue()
    {
        // 3.3: 매출을 직접 깎는 페널티는 두지 않는다. 이 클래스가 매출을 아예 모르는 것이
        // 그 규칙을 지키는 방법이다 — 판매가 계산은 SalePrice 혼자 한다.
        var ledger = AtStreak(3);
        var type = typeof(TeamLedger);
        foreach (var member in type.GetMembers())
        {
            var name = member.Name.ToLowerInvariant();
            Assert.IsFalse(name.Contains("revenue") || name.Contains("price") || name.Contains("sale"),
                $"TeamLedger가 매출에 손대고 있다: {member.Name}");
        }
        Assert.AreEqual(RentPenalty.Tier3, ledger.Penalty);
    }

    [Test]
    public void PayingRentLiftsThePenaltyOnTheNextSettlement()
    {
        var ledger = AtStreak(2);
        Assert.AreEqual(RentPenalty.Tier2, ledger.Penalty);

        ledger.Rent.Settle(3, 10000);
        ledger.ApplySettledPenalty();
        Assert.AreEqual(RentPenalty.None, ledger.Penalty, "다음 날 임대료를 내면 해제된다");
        Assert.AreEqual(1f, ledger.MoveSpeedScale, 0.0001f);
    }

    [Test]
    public void ThePenaltyOnlyMovesAtSettlement()
    {
        // 낮 도중에 페널티가 바뀌면 발밑에서 속도가 달라진다. 정산 때만 움직인다.
        var ledger = new TeamLedger();
        ledger.Rent.Settle(1, 0);
        Assert.AreEqual(RentPenalty.None, ledger.Penalty, "아직 적용 전");

        ledger.ApplySettledPenalty();
        Assert.AreEqual(RentPenalty.Tier1, ledger.Penalty);
    }

    [Test]
    public void LedgersAreIndependent()
    {
        // 한 팀의 빚이 다른 팀을 벌하던 결함이 이 격리로 닫힌다.
        var a = AtStreak(3);
        var b = new TeamLedger();

        Assert.AreEqual(0.8f, a.MoveSpeedScale, 0.0001f);
        Assert.AreEqual(1f, b.MoveSpeedScale, 0.0001f);
        Assert.AreEqual(0, b.Rent.Debt);
    }
}
