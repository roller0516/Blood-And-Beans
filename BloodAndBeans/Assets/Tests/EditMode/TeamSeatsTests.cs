using NUnit.Framework;

/// 로비가 고른 팀을 서버가 검사하는 규칙 (기획서 10장: 팀 인원은 로비가 정한다).
/// 4팀 × 2인 = 8명이 이 프로젝트가 지원하는 최대 구성이다.
public class TeamSeatsTests
{
    const int Teams = 4;
    const int PerTeam = 2;

    static TeamSeats Fresh() => new(Teams, PerTeam);

    [Test]
    public void CapacityIsTeamsTimesPlayersPerTeam()
    {
        var seats = Fresh();
        Assert.AreEqual(Teams, seats.TeamCount);
        Assert.AreEqual(PerTeam, seats.CapacityPerTeam);
        Assert.AreEqual(Teams * PerTeam, seats.Capacity);
    }

    [Test]
    public void PreferredTeamWinsWhileItHasRoom()
    {
        var seats = Fresh();
        Assert.AreEqual(2, seats.Take(2));
        Assert.AreEqual(2, seats.Take(2));
        Assert.AreEqual(2, seats.OccupancyOf(2));
    }

    [Test]
    public void AFullTeamFallsBackToTheEmptiestOne()
    {
        var seats = Fresh();
        seats.Take(0);
        seats.Take(0);

        // 팀 0은 찼다. 나머지 셋은 비어 있으므로 가장 낮은 번호가 이긴다.
        Assert.AreEqual(1, seats.Take(0));
        Assert.AreEqual(2, seats.OccupancyOf(0));
    }

    [Test]
    public void RangeIsCheckedSoAClientCannotInventATeam()
    {
        var seats = Fresh();
        Assert.AreEqual(0, seats.Take(Teams));      // 없는 팀
        Assert.AreEqual(1, seats.Take(-7));         // 음수
    }

    /// 선호가 없으면 가장 빈 팀. 예전 라운드 로빈과 결과가 같아야 2인 세션에서도 팀이
    /// 둘로 갈리고 팀 격리를 시험할 수 있다.
    [Test]
    public void NoPreferenceSpreadsPlayersAcrossTeams()
    {
        var seats = Fresh();
        for (var i = 0; i < Teams; i++)
            Assert.AreEqual(i, seats.Take(TeamSeats.NoPreference));
    }

    [Test]
    public void AFullRoomRefusesTheNextPlayer()
    {
        var seats = Fresh();
        for (var i = 0; i < seats.Capacity; i++)
            Assert.AreNotEqual(TeamSeats.NoSeat, seats.Take(TeamSeats.NoPreference));

        Assert.AreEqual(TeamSeats.NoSeat, seats.Take(TeamSeats.NoPreference));
        Assert.AreEqual(TeamSeats.NoSeat, seats.Take(0));
    }

    /// 나간 자리를 비우지 않으면 재접속만으로 방이 영영 가득 찬다.
    [Test]
    public void LeavingFreesTheSeat()
    {
        var seats = Fresh();
        for (var i = 0; i < seats.Capacity; i++) seats.Take(TeamSeats.NoPreference);

        seats.Release(3);
        Assert.AreEqual(3, seats.Take(3));
        Assert.AreEqual(TeamSeats.NoSeat, seats.Take(TeamSeats.NoPreference));
    }

    [Test]
    public void ReleasingAnEmptyOrUnknownTeamChangesNothing()
    {
        var seats = Fresh();
        seats.Release(0);
        seats.Release(Teams + 5);
        Assert.AreEqual(0, seats.OccupancyOf(0));
        Assert.AreEqual(0, seats.Take(0));
    }
}
