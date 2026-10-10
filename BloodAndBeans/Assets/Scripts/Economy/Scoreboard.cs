using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// 전원에게 공개되는 팀별 정보판. 누적 매출과 실시간 순위(기획서 3.1)를 든다.
///
/// 판 전체의 값이라 카페마다 두지 않는다. 재료·설비는 카페가 들고 있다.
public class Scoreboard : NetworkBehaviour
{
    // 팀 수는 MatchDirector에서 온다. 여기서 계산대를 세던 코드가 "팀이 몇 개인가"에
    // 각자 답하던 여섯 곳 중 하나였다 (아키텍처_v1.0.md §1.4).
    readonly NetworkList<int> revenue = new();

    public int TeamCount => revenue.Count;
    public int RevenueOf(int team) => revenue[team];

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        revenue.Clear();                       // 다시 스폰됐을 때 팀이 덧쌓이면 안 된다
        var director = MatchDirector.Instance;
        var teams = director != null ? director.TeamCount : 1;
        for (var i = 0; i < teams; i++) revenue.Add(0);
    }

    /// 서버 전용. amount는 SalePrice.Calculate가 낸 값을 그대로 받는다.
    public void AddSale(int team, int amount)
    {
        if (!IsServer) return;
        revenue[team] += amount;
    }

    /// 매출이 많은 순서의 팀 인덱스. ponytail: 호출마다 리스트를 할당한다.
    /// 팀이 4개 이하이고 최악이어도 프레임당 표시 갱신 한 번이라 괜찮다.
    public List<int> Ranking()
    {
        var order = new List<int>(revenue.Count);
        for (var i = 0; i < revenue.Count; i++) order.Add(i);
        order.Sort((a, b) => revenue[b].CompareTo(revenue[a]));
        return order;
    }
}
