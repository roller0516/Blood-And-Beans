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
        Assert.AreEqual(1f, ledger.CraftSpeedScale, 0.0001f);
        Assert.IsFalse(ledger.MachineDown);
        Assert.IsFalse(ledger.BreaksDish);
        Assert.AreEqual(1f, ledger.VisionScale, 0.0001f);
        Assert.AreEqual(1f, ledger.BoxOpenScale, 0.0001f);
        Assert.IsFalse(ledger.WeightBandShifted);
    }

    [Test]
    public void FirstMissSlowsCraftingAndShrinksVisionOnly()
    {
        var ledger = AtStreak(1);
        Assert.AreEqual(RentPenalty.Tier1, ledger.Penalty);

        // 낮: 제작 속도 10% 감소 = 조리 시간 1.1배.
        Assert.AreEqual(1.10f, ledger.CraftSpeedScale, 0.0001f);
        Assert.IsFalse(ledger.MachineDown, "머신 불통은 2회부터다");
        Assert.IsFalse(ledger.BreaksDish, "그릇 파손은 3회부터다");

        // 밤: 시야만 줄고 개봉 속도와 무게는 아직 멀쩡하다.
        Assert.Less(ledger.VisionScale, 1f);
        Assert.AreEqual(1f, ledger.BoxOpenScale, 0.0001f);
        Assert.IsFalse(ledger.WeightBandShifted);
    }

    [Test]
    public void SecondMissAddsAMachineAndSlowerOpening()
    {
        var ledger = AtStreak(2);
        Assert.AreEqual(RentPenalty.Tier2, ledger.Penalty);
        Assert.IsTrue(ledger.MachineDown);
        Assert.IsFalse(ledger.BreaksDish);
        Assert.Greater(ledger.BoxOpenScale, 1f, "개봉이 느려져야 한다");
        Assert.IsFalse(ledger.WeightBandShifted);
    }

    [Test]
    public void ThirdMissAddsADishAndAWeightBand()
    {
        var ledger = AtStreak(3);
        Assert.AreEqual(RentPenalty.Tier3, ledger.Penalty);
        Assert.IsTrue(ledger.MachineDown, "3단계는 2단계를 포함한다");
        Assert.IsTrue(ledger.BreaksDish);
        Assert.IsTrue(ledger.WeightBandShifted);
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
        Assert.IsFalse(ledger.MachineDown);
    }

    [Test]
    public void ThePenaltyOnlyMovesAtSettlement()
    {
        // 낮 도중에 페널티가 바뀌면 머신이 갑자기 살아나거나 죽는다. 정산 때만 움직인다.
        var ledger = new TeamLedger();
        ledger.Rent.Settle(1, 0);
        Assert.AreEqual(RentPenalty.None, ledger.Penalty, "아직 적용 전");

        ledger.ApplySettledPenalty();
        Assert.AreEqual(RentPenalty.Tier1, ledger.Penalty);
    }

    [Test]
    public void LedgersAreIndependent()
    {
        // 한 팀의 빚이 다른 팀의 그릇을 깨던 결함이 이 격리로 닫힌다.
        var a = AtStreak(3);
        var b = new TeamLedger();

        Assert.IsTrue(a.BreaksDish);
        Assert.IsFalse(b.BreaksDish);
        Assert.AreEqual(0, b.Rent.Debt);
    }
}
