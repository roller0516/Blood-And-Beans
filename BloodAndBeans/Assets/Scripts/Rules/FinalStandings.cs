using System.Collections.Generic;

/// 최종 결산 (기획서 3.1). 판정 지표는 누적 판매 매출이고, 마지막 낮이 끝나면 1위 팀이
/// 승리한다.
///
/// 순위 나열은 이미 `Scoreboard.Ranking()`이 한다. 여기 있는 것은 그것으로 답할 수 없는
/// 것 하나 — **누가 이겼는가** — 뿐이다. 씬도 네트워크도 없이 기획서와 대조할 수 있게
/// BB.Rules에 둔다.
public static class FinalStandings
{
    /// 최고 매출과 같은 매출을 가진 팀 전부. 보통 하나다.
    ///
    /// 기획서 3.1에는 동점 규칙이 없다. 그래서 임의로 순위를 가르지 않고 공동 1위로
    /// 돌려준다 — 팀 번호가 작은 쪽을 이기게 하는 것은 기획에 없는 규칙을 지어내는 것이다.
    /// 규칙이 정해지면 이 함수 하나만 고치면 된다.
    ///
    /// 매출이 전부 0이어도 공동 1위다. 아무도 못 팔았다는 사실이지 무승부가 아닌 것은
    /// 아니므로, 판정을 읽는 쪽이 `IsTie`로 갈라 쓴다.
    public static List<int> WinnersOf(IReadOnlyList<int> revenueByTeam)
    {
        var winners = new List<int>();
        if (revenueByTeam == null || revenueByTeam.Count == 0) return winners;

        var best = revenueByTeam[0];
        for (var team = 1; team < revenueByTeam.Count; team++)
            if (revenueByTeam[team] > best) best = revenueByTeam[team];

        for (var team = 0; team < revenueByTeam.Count; team++)
            if (revenueByTeam[team] == best) winners.Add(team);

        return winners;
    }

    /// 1위가 둘 이상인가. 팀이 하나뿐인 판(1인 1팀)은 동점이 아니다 — 겨룰 상대가 없다.
    public static bool IsTie(IReadOnlyList<int> revenueByTeam) =>
        revenueByTeam != null && revenueByTeam.Count > 1 &&
        WinnersOf(revenueByTeam).Count > 1;
}
