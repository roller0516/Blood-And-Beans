using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// 전원에게 공개되는 팀별 정보판. 누적 매출과 실시간 순위(기획서 3.1), 그리고 카페
/// 입구 등이 읽는 위급 신호(5.7.6)를 든다.
///
/// 둘 다 기획서가 「전원에게 보인다」고 못 박은 값이라 같은 자리에 둔다. 카페는 자기
/// 팀에만 복제되므로(3.4) 남의 카페 손님 상태를 알 통로가 여기 말고는 없다.
/// 재료·설비·캐릭터는 비공개로 남는다 — 그것들이 들어올 자리는 아니다.
public class Scoreboard : NetworkBehaviour
{
    // 팀 수는 MatchDirector에서 온다. 여기서 계산대를 세던 코드가 "팀이 몇 개인가"에
    // 각자 답하던 여섯 곳 중 하나였다 (아키텍처_v1.0.md §1.4).
    readonly NetworkList<int> revenue = new();

    /// 위급한 손님이 있는 팀의 비트마스크 (기획서 5.7.6). 팀당 NetworkList를 하나 더 두는
    /// 것보다 값 하나가 싸고, 팀은 최대 4이라 비트로 충분하다.
    readonly NetworkVariable<int> urgentTeams = new();

    public int TeamCount => revenue.Count;
    public int RevenueOf(int team) => revenue[team];

    /// 이 팀 카페에 위급한 손님이 있는가. 전원이 읽는다.
    public bool UrgentOf(int team) => team >= 0 && (urgentTeams.Value & (1 << team)) != 0;

    /// 대기열이 상태가 바뀔 때만 부른다.
    public void SetUrgentServer(int team, bool urgent)
    {
        if (!IsServer || team < 0 || team >= 32) return;
        var bit = 1 << team;
        urgentTeams.Value = urgent ? urgentTeams.Value | bit : urgentTeams.Value & ~bit;
    }

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
