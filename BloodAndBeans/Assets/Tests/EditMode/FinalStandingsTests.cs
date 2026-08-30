using NUnit.Framework;

/// 최종 결산 판정 (기획서 3.1) — 1위는 누구고 동점은 어떻게 되는가.
public class FinalStandingsTests
{
    [Test]
    public void HighestRevenueWins()
    {
        var winners = FinalStandings.WinnersOf(new[] { 120, 340, 90 });

        Assert.AreEqual(1, winners.Count);
        Assert.AreEqual(1, winners[0]);
        Assert.IsFalse(FinalStandings.IsTie(new[] { 120, 340, 90 }));
    }

    [Test]
    public void EqualTopRevenueIsSharedFirstPlace()
    {
        // 기획서 3.1에 동점 규칙이 없다. 팀 번호가 작은 쪽을 이기게 하는 것은 기획에
        // 없는 규칙을 지어내는 것이므로 공동 1위로 둔다.
        var revenue = new[] { 500, 200, 500 };
        var winners = FinalStandings.WinnersOf(revenue);

        Assert.AreEqual(2, winners.Count);
        Assert.Contains(0, winners);
        Assert.Contains(2, winners);
        Assert.IsTrue(FinalStandings.IsTie(revenue));
    }

    [Test]
    public void NoSalesAtAllIsStillATie()
    {
        var revenue = new[] { 0, 0 };

        Assert.AreEqual(2, FinalStandings.WinnersOf(revenue).Count);
        Assert.IsTrue(FinalStandings.IsTie(revenue), "아무도 못 팔았어도 무승부다");
    }

    [Test]
    public void SingleTeamMatchIsNotATie()
    {
        // 1인 1팀도 가능하다. 겨룰 상대가 없는 것은 무승부가 아니다.
        var revenue = new[] { 0 };

        Assert.AreEqual(1, FinalStandings.WinnersOf(revenue).Count);
        Assert.IsFalse(FinalStandings.IsTie(revenue));
    }

    [Test]
    public void EmptyBoardHasNoWinner()
    {
        Assert.AreEqual(0, FinalStandings.WinnersOf(new int[0]).Count);
        Assert.AreEqual(0, FinalStandings.WinnersOf(null).Count);
    }
}
