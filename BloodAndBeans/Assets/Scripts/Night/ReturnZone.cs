using Unity.Netcode;
using UnityEngine;

/// 팀의 복귀 지점 (기획서 6.8). 밤이 끝나는 순간 자기 팀 구역 안에 있지 않은 사람은
/// 들고 있던 것의 절반을 잃고, 살아남은 나머지가 내일의 팀 재고가 된다 (기획서 2장).
/// 이 입고 처리가 생기기 전에는 밤의 수확이 그냥 버려져서 코어 루프가 끊겨 있었다.
public class ReturnZone : NetworkBehaviour
{
    [SerializeField] float radius = 4f;

    Cafe cafe;
    MatchDirector director;

    /// 팀 번호가 아니라 카페를 들고 있는다. MatchDirector는 자기 Awake에서 팀 번호를
    /// 배정하는데 두 Awake 사이의 순서에 기대면 안 된다. 부모를 거슬러 올라가는 방식은
    /// 어느 쪽이든 안정적이다. 카페를 복제하면 직렬화된 0이 두 구역에 그대로 복사돼서,
    /// 팀 1의 구역이 팀 0의 플레이어를 판정해 자기 문 앞에 서 있는데도 수확 절반을 빼앗았다.
    void Awake() => cafe = Cafe.Of(this);

    int TeamId => cafe != null ? cafe.TeamId : -1;

    /// `phase.Current`를 폴링하지 않고 페이즈 이벤트에서 정산하는 것이 TransitionLedger와의
    /// 순서를 확정해 준다. PhaseEntered는 GamePhase.Enter 안에서 발생하므로, 어떤 Update가
    /// 새 페이즈를 관측하고 팀 재고로 예보를 뽑기 전에 모든 입고가 끝나 있다 (기획서 5.5 규칙 3).
    public override void OnNetworkSpawn()
    {
        director = MatchDirector.Instance;
        if (IsServer && director != null) director.Phase.PhaseEntered += OnPhaseEntered;
    }

    public override void OnNetworkDespawn()
    {
        if (director != null) director.Phase.PhaseEntered -= OnPhaseEntered;
    }

    void OnPhaseEntered(Phase p)
    {
        if (!IsServer || p != Phase.Transition) return;
        Settle();
    }

    void Settle()
    {
        var team = TeamId;
        if (team < 0) return;      // 팀이 배정되지 않은 구역은 아무도 판정하지 않는다

        var stock = cafe != null ? cafe.Stock : null;

        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            var player = client.PlayerObject;
            if (player == null || TeamOf(player) != team) continue;

            var inv = player.GetComponent<PlayerInventory>();
            if (inv == null) continue;

            // 구역에 못 들어오면 절반을 잃는다. 완전 소실이라 아무도 주울 수 없다 (6.8).
            if (Vector3.Distance(player.transform.position, transform.position) > radius)
            {
                inv.LoseHalfServer();
                SnapToZoneServer(player);
            }

            var haul = inv.DrainServer();
            if (stock == null) continue;
            for (var i = 0; i < haul.Count; i++)
            {
                // 상비 재료는 재고로 세지 않는다 (기획서 7.1) — 선반이 무한으로 준다.
                if (Ingredients.IsStaple(haul[i])) continue;
                stock.DepositServer(haul[i]);
            }
        }
    }

    /// 귀환에 실패한 사람을 구역 안으로 들여놓는다. 페널티를 매긴 *뒤*라 6.8의 대가는
    /// 그대로 치른다. 이걸 하지 않으면 숲에 남은 채로 낮이 시작되는데, 낮 쪽 진입점은
    /// 전부 거리 검사를 하므로 그 플레이어는 낮 2분 동안 아무것도 할 수 없다.
    ///
    /// y는 플레이어의 것을 유지한다. 구역은 수평 반경이고, 캡슐 중심을 구역 오브젝트
    /// 높이로 끌어올리면 지면에 뜬다.
    void SnapToZoneServer(NetworkObject player)
    {
        PlayerTeleport.ToServer(player.gameObject, new Vector3(
            transform.position.x, player.transform.position.y, transform.position.z));
    }

    static int TeamOf(NetworkObject player)
    {
        var t = player != null ? player.GetComponent<PlayerTeam>() : null;
        return t != null ? t.Team : -1;
    }
}
