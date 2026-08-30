using Unity.Netcode;
using UnityEngine;

/// 밤이 끝났을 때 이 사람에게 무슨 일이 있었는가 (기획서 6.8의 세 갈래).
public enum ReturnOutcome
{
    /// 소환 위치 O + 가방 소지 O
    Returned,
    /// 소환 위치 X + 가방 소지 O
    PartialLoss,
    /// 가방 소지 X (소환 위치와 무관)
    BagLost,
}

/// 팀의 복귀 지점 (기획서 6.8). 밤이 끝나는 순간 자기 팀 구역 안에 있지 않은 사람은
/// 들고 있던 것의 일부를 잃고, 살아남은 나머지가 내일의 팀 재고가 된다 (기획서 2장).
///
/// **자리는 이 오브젝트의 transform이 아니라 그 팀의 밤 시작 지점이다** (기획서 6.8:
/// "소환 위치"). 이 컴포넌트는 카페 프리팹의 자식이라 transform은 카페 위에 있는데,
/// 밤에는 `MatchDirector.NightSpawnPosition`이 플레이어를 숲 모서리에 세운다 — 그
/// 좌표로 판정하면 아무도 서 본 적 없는 카페 앞이 기준이 되어 전원이 귀환에 실패한다.
/// 그 값은 씬 오브젝트의 직렬화 값으로만 계산되므로 서버와 클라이언트가 같은 답을
/// 내고, 복제할 것이 없다.
/// 이 입고 처리가 생기기 전에는 밤의 수확이 그냥 버려져서 코어 루프가 끊겨 있었다.
public class ReturnZone : NetworkBehaviour
{
    [SerializeField] float radius = 4f;

    /// 가방은 메고 있지만 소환 위치 밖에서 밤이 끝났을 때 잃는 비율 (기획서: 일부(n%) 소실).
    [SerializeField, Range(0f, 1f)] float missedReturnLoss = 0.5f;

    Cafe cafe;
    MatchDirector director;

    /// 이 클라이언트가 마지막 밤에 받은 결과. 읽는 쪽(`MatchFlow`)이 소비한다.
    /// 이벤트가 아니라 소비하는 값인 이유는 팝업을 띄우는 쪽이 늦게 붙기 때문이다 —
    /// 카페는 런타임에 복제되고, 그때 아무도 구독하고 있지 않으면 결과가 그냥 사라진다.
    public bool HasResult { get; private set; }
    public ReturnOutcome Outcome { get; private set; }
    public int KeptCount { get; private set; }
    public int LostCount { get; private set; }

    /// 기획서 6.8의 "n%". 문구에 그대로 들어간다.
    public int LossPercent => Mathf.RoundToInt(missedReturnLoss * 100f);

    public void ConsumeResult() => HasResult = false;

    /// 팀 번호가 아니라 카페를 들고 있는다. MatchDirector는 자기 Awake에서 팀 번호를
    /// 배정하는데 두 Awake 사이의 순서에 기대면 안 된다. 부모를 거슬러 올라가는 방식은
    /// 어느 쪽이든 안정적이다. 카페를 복제하면 직렬화된 0이 두 구역에 그대로 복사돼서,
    /// 팀 1의 구역이 팀 0의 플레이어를 판정해 자기 문 앞에 서 있는데도 수확 절반을 빼앗았다.
    void Awake() => cafe = Cafe.Of(this);

    int TeamId => cafe != null ? cafe.TeamId : -1;

    /// 이 팀이 돌아와야 하는 자리. 슬롯 0을 팀의 기준점으로 쓴다 — 같은 팀의 두 자리는
    /// `spawnSlotSpacing`(2m)만큼만 떨어져 있어서 `radius` 안에 함께 들어온다.
    /// 정산과 HUD 마커가 같은 함수를 읽어야 표시와 판정이 어긋나지 않는다.
    public Vector3? Center
    {
        get
        {
            var team = TeamId;
            return director != null && team >= 0
                ? director.NightSpawnPosition(team, 0)
                : (Vector3?)null;
        }
    }

    /// `phase.Current`를 폴링하지 않고 페이즈 이벤트에서 정산하는 것이 TransitionLedger와의
    /// 순서를 확정해 준다. PhaseEntered는 GamePhase.Enter 안에서 발생하므로, 어떤 Update가
    /// 새 페이즈를 관측하고 팀 재고로 예보를 뽑기 전에 모든 입고가 끝나 있다 (기획서 5.5 규칙 3).
    public override void OnNetworkSpawn()
    {
        director = MatchDirector.Instance;

        // 서버·클라이언트 양쪽에서 건다. 자리를 옮기는 것은 보는 쪽에도 필요한 일이고,
        // 기준점이 씬 오브젝트의 직렬화 값으로만 계산되므로 두 쪽이 같은 답을 낸다.
        if (director != null) director.Phase.PhaseEntered += OnPhaseEntered;
        PlaceAtSpawnPoint();
    }

    public override void OnNetworkDespawn()
    {
        if (director != null) director.Phase.PhaseEntered -= OnPhaseEntered;
    }

    void OnPhaseEntered(Phase p)
    {
        // 팀 번호는 카페의 NetworkVariable이라 스폰 시점에는 아직 0일 수 있다. 밤이
        // 시작될 때 한 번 더 놓으면 정산(전환)보다 항상 먼저다.
        if (p == Phase.Night) PlaceAtSpawnPoint();
        else if (IsServer && p == Phase.Transition) Settle(p);
    }

    /// 보이는 판과 트리거를 실제 귀환 자리로 옮긴다. 판정만 옮기고 이걸 하지 않으면
    /// 돌아가야 할 자리에는 아무것도 없고 판은 카페 앞에 남는다.
    ///
    /// y는 건드리지 않는다. 프리팹에 손으로 맞춰 둔 접지 높이이고, 여기서 새 값을
    /// 지어내면 판이 지면에 파묻히거나 뜬다 — 중력이 없어 스스로 내려오지도 않는다.
    void PlaceAtSpawnPoint()
    {
        var center = Center;
        if (!center.HasValue) return;

        transform.position = new Vector3(center.Value.x, transform.position.y, center.Value.z);
    }

    void Settle(Phase p)
    {
        var team = TeamId;
        if (team < 0) return;      // 팀이 배정되지 않은 구역은 아무도 판정하지 않는다

        var center = Center;
        if (!center.HasValue) return;   // 기준점을 모르면 판정하지 않는다. 전원 실패보다 낫다

        var stock = cafe != null ? cafe.Stock : null;

        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            var player = client.PlayerObject;
            if (player == null || TeamOf(player) != team) continue;

            // 귀환 판정에 쓸 위치를 옮기기 전에 읽는다.
            var inZone = Vector3.Distance(player.transform.position, center.Value) <= radius;

            // 그리고 카페로 들여보낸다 — 정산과 배치를 한 자리에 두어야 순서가 확정된다.
            // 페이즈 이벤트에서 각자 자기 자리를 찾아가게 하면 이 정산보다 먼저 도착하는
            // 순서가 생기고, 제대로 귀환한 사람이 전부 실패로 걸린다.
            //
            // 정산에서 빠지는 사람(인벤토리 없음)보다 앞에 둔다. 숲에 남으면 낮 진입점이
            // 전부 거리 검사라 그 플레이어는 낮 내내 아무것도 할 수 없다.
            player.GetComponent<PlayerTeam>()?.MoveToPhaseStartServer(p);

            var inv = player.GetComponent<PlayerInventory>();
            if (inv == null) continue;

            // 정산은 세 갈래다 (기획서 5장 귀환 룰).
            // 소환 위치 O + 가방 O → 전량 귀환.
            // 소환 위치 X + 가방 O → 일부 소실. 완전 소실이라 아무도 주울 수 없다 (6.8).
            // 가방 X (묻어 두고 회수하지 않음) → 위치와 무관하게 전량 소실.
            if (!inv.HasBag)
            {
                ReportServer(client.ClientId, ReturnOutcome.BagLost, 0, inv.Count);
                inv.ClearServer();
                continue;
            }

            var before = inv.Count;
            if (!inZone) inv.LoseShareServer(missedReturnLoss);

            ReportServer(client.ClientId,
                inZone ? ReturnOutcome.Returned : ReturnOutcome.PartialLoss,
                inv.Count, before - inv.Count);

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

    void ReportServer(ulong clientId, ReturnOutcome outcome, int kept, int lost) =>
        ReportResultRpc(outcome, kept, lost, RpcTarget.Single(clientId, RpcTargetUse.Temp));

    /// 자기 결과만 자기에게 보낸다. 전체에 뿌리면 남의 수확량이 그대로 새어 나가는데,
    /// 기획서 3.1은 매출 말고는 비공개라고 못박았다.
    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    void ReportResultRpc(ReturnOutcome outcome, int kept, int lost, RpcParams p = default)
    {
        Outcome = outcome;
        KeptCount = kept;
        LostCount = lost;
        HasResult = true;
    }

    static int TeamOf(NetworkObject player)
    {
        var t = player != null ? player.GetComponent<PlayerTeam>() : null;
        return t != null ? t.Team : -1;
    }
}
