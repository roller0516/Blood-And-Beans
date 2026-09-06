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
/// **숲에 서 있는 오브젝트다.** 자리는 그 팀의 밤 시작 지점이고(기획서 6.8 "소환 위치"),
/// `MatchDirector`가 카페와 함께 팀 수만큼 스폰한다. 카페 프리팹의 자식이 아니다 —
/// 카페는 숲 바깥 격자에 서고 이쪽은 숲 모서리에 선다.
///
/// 자기 팀에만 복제된다 (`SpawnWithObservers = false`). 남의 귀환 지점은 볼 이유가 없다.
[RequireComponent(typeof(NetworkObject))]
public class ReturnZone : NetworkBehaviour
{
    [SerializeField] float radius = 4f;

    /// 가방은 메고 있지만 소환 위치 밖에서 밤이 끝났을 때 잃는 비율 (기획서: 일부(n%) 소실).
    [SerializeField, Range(0f, 1f)] float missedReturnLoss = 0.5f;

    /// 스폰 페이로드에 실려 모든 피어가 같은 값을 받는다 (`Cafe.team`과 같은 방식).
    readonly NetworkVariable<int> team = new(-1);
    int pendingServerTeam = -1;

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

    /// 서버가 Spawn 직전에 부른다. 스폰 뒤에 쓰면 그 값은 다음 틱의 델타로 가고,
    /// 그동안 이 구역은 팀 미상으로 남는다 (`Cafe.AssignTeamServer`와 같은 이유).
    public void AssignTeamServer(int value) => pendingServerTeam = value;

    public int TeamId => team.Value;

    /// 재고를 넣을 카페. 팀 번호로 찾는다 — 더 이상 부모가 아니다.
    Cafe Cafe => director != null ? director.CafeOf(TeamId) : null;

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

    /// 재지급과 밤 종료 정산이 같은 귀환 반경을 사용한다 (기획서 6.8).
    public bool Contains(Vector3 position)
    {
        var center = Center;
        return center.HasValue && Vector3.Distance(position, center.Value) <= radius;
    }

    /// `phase.Current`를 폴링하지 않고 페이즈 이벤트에서 정산하는 것이 TransitionLedger와의
    /// 순서를 확정해 준다. PhaseEntered는 GamePhase.Enter 안에서 발생하므로, 어떤 Update가
    /// 새 페이즈를 관측하기 전에 모든 입고가 끝나 있다.
    ///
    /// 밤 다음은 낮이다 (밤 -> 낮 -> 전환). 그래서 귀환 판정도 낮이 시작될 때 한다.
    public override void OnNetworkSpawn()
    {
        if (IsServer && pendingServerTeam >= 0) team.Value = pendingServerTeam;

        director = MatchDirector.Instance;
        if (director == null)
        {
            CDebug.LogError($"{name}: 씬에 {nameof(MatchDirector)}가 없다. 귀환 판정이 돌지 않는다.", this);
            return;
        }

        director.RegisterZone(this);
        director.Phase.PhaseEntered += OnPhaseEntered;

        // 카메라 컬링이 레이어로 팀을 가른다. 프리팹 하나를 여러 팀이 쓰므로 레이어는
        // 스폰 시점에 정해야 한다 (`Cafe.OnNetworkSpawn`과 같다).
        TeamVision.ApplyTeamLayer(gameObject, team.Value);

        // 서버·클라이언트 양쪽에서 놓는다. 기준점이 팀 번호만으로 계산되므로 두 쪽이
        // 같은 답을 낸다.
        PlaceAtSpawnPoint();
    }

    public override void OnNetworkDespawn()
    {
        if (director == null) return;
        director.UnregisterZone(this);
        director.Phase.PhaseEntered -= OnPhaseEntered;
    }

    void OnPhaseEntered(Phase p)
    {
        // 팀 번호는 카페의 NetworkVariable이라 스폰 시점에는 아직 0일 수 있다. 밤이
        // 시작될 때 한 번 더 놓으면 귀환 판정(낮 진입)보다 항상 먼저다.
        if (p == Phase.Night) PlaceAtSpawnPoint();
        else if (IsServer && p == Phase.Day) Settle(p);
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

        var stock = Cafe != null ? Cafe.Stock : null;

        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            var player = client.PlayerObject;
            if (player == null || TeamOf(player) != team) continue;

            // 귀환 판정에 쓸 위치를 옮기기 전에 읽는다.
            var inZone = Contains(player.transform.position);

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
                // `inv.Count`는 이미 0이다 — 묻는 순간 비웠다. 실제로 잃은 개수는
                // `BuriedLossCount`가 그 순간 값으로 따로 들고 있다.
                ReportServer(client.ClientId, ReturnOutcome.BagLost, 0, inv.BuriedLossCount);
                inv.ClearServer();
                continue;
            }

            var before = inv.Count;
            if (!inZone) inv.LoseShareServer(missedReturnLoss);

            ReportServer(client.ClientId,
                inZone ? ReturnOutcome.Returned : ReturnOutcome.PartialLoss,
                inv.Count, before - inv.Count + inv.BuriedLossCount);

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
