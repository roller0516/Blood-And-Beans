using Unity.Netcode;
using UnityEngine;

public enum StationState { Idle, Cooking, Gauge, Product }

/// 재료 투입 -> 자동 진행 -> 완성 게이지 (기획서 5.1).
/// 커피 머신과 오븐은 제작 시간만 다르므로 나머지는 전부 여기 있다.
///
/// **조리 레인이 둘까지 있다.** 기본은 하나이고, 「2구 머신」·「오븐 확장」을 설치하면
/// 둘이 된다 (기획서 8장: "한 대에서 2잔 동시 제조" / "디저트 2개 동시 굽기").
/// 레인마다 재료·조리 시계·완성품을 따로 들고 독립적으로 돈다.
///
/// **완성 게이지는 레인이 늘어도 하나다.** 기획서 5.2는 게이지를 조리가 끝나면 뜨는
/// 바늘 하나로 정했고, 완성 상태가 10초간 유지되므로 둘이 거의 동시에 끝나도 순서대로
/// 처리할 시간이 있다. 두 바늘이 동시에 왕복하면 F 한 번이 어느 쪽을 멈추는지 화면에서
/// 읽을 수 없다 — `CompletionGauge.TryStopLocalClient`가 이미 "가장 오래된 것"으로
/// 그 모호함을 처리하고 있고, 같은 기계 안에서 그것을 두 번 겹칠 이유가 없다.
[RequireComponent(typeof(CompletionGauge))]
public class Station : NetworkBehaviour, IInteractable, IItemHolder
{
    /// 한 기계가 가질 수 있는 최대 레인 수 (기획서 8장: 2잔·2개 동시).
    public const int MaxLanes = 2;

    [SerializeField] protected float cookSeconds = 5f;
    [SerializeField] float reach = 2.5f;

    /// 레인 하나가 받는 재료 칸 수.
    [SerializeField] int maxIngredients = 4;

    readonly NetworkVariable<bool> disabled = new();

    public bool Disabled => disabled.Value;

    /// 임대료 페널티 2단계부터 그날 하루 기계 한 대가 멈춘다 (기획서 3.3).
    public void SetDisabledServer(bool value)
    {
        if (!IsServer) return;
        disabled.Value = value;
    }

    // --- 레인 상태. NGO는 NetworkVariable을 배열로 선언할 수 없어 두 벌을 편다.
    //     읽고 쓰는 곳은 아래 Loaded/State/DoneAt/ProductView 넷뿐이다. ---

    readonly NetworkList<int> loaded0 = new();
    readonly NetworkList<int> loaded1 = new();
    readonly NetworkVariable<StationState> state0 = new();
    readonly NetworkVariable<StationState> state1 = new();
    readonly NetworkVariable<double> doneAt0 = new();
    readonly NetworkVariable<double> doneAt1 = new();

    /// 완성품의 표시용 사본. `products`는 서버만 들고 있어서(`Recipe`가 관리 배열이다)
    /// 예전에는 클라이언트가 "완성됐다"는 상태만 알고 **무엇이 완성됐는지는 몰랐다** —
    /// 기계 위의 완성품을 그릴 방법이 아예 없었다. 손·조리대와 같은 수단으로 맞춘다.
    readonly NetworkVariable<CarryView> productView0 = new(CarryView.Nothing);
    readonly NetworkVariable<CarryView> productView1 = new(CarryView.Nothing);

    NetworkList<int> Loaded(int lane) => lane == 0 ? loaded0 : loaded1;
    NetworkVariable<StationState> State(int lane) => lane == 0 ? state0 : state1;
    NetworkVariable<double> DoneAt(int lane) => lane == 0 ? doneAt0 : doneAt1;
    NetworkVariable<CarryView> ProductView(int lane) => lane == 0 ? productView0 : productView1;

    /// 서버 측. 레인별로 누군가 집어 가기를 기다리는 완성품.
    readonly HeldItem[] products = new HeldItem[MaxLanes];

    /// 지금 게이지를 쓰고 있는 레인. 없으면 -1. 서버 전용이다 — 게이지 결과가 어느 레인의
    /// 것인지는 서버만 알면 된다.
    int gaugeLane = NoLane;
    const int NoLane = -1;

    CompletionGauge gauge;
    Cafe ownerCafe;

    /// 손이 닿는지 재는 기준면. 주기 실행 밖에서 한 번만 찾는다 (AGENTS.md 참조와 결합도).
    Collider surface;

    void Awake() => surface = GetComponentInChildren<Collider>(true);

    /// 조립 루트는 전역이 아니라 소속 카페에서 받는다. 설비마다 따로 찾으면 카페별로
    /// 다른 답이 나올 여지가 생긴다 (아키텍처_v1.0.md §1.4).
    Cafe Owner => ownerCafe != null ? ownerCafe : (ownerCafe = Cafe.Of(this));
    MatchDirector Director => Owner != null ? Owner.Director : null;

    /// 이 기계를 2레인으로 만드는 업그레이드. 없으면 늘 1레인이다 (기획서 8장).
    protected virtual UpgradeId? ParallelUpgrade => null;

    /// 지금 쓸 수 있는 레인 수.
    public int Lanes =>
        ParallelUpgrade.HasValue && Owner != null && Owner.HasUpgrade(ParallelUpgrade.Value)
            ? MaxLanes : 1;

    public float Reach => reach;

    /// 조리 중인 레인 가운데 가장 먼저 끝나는 것의 남은 시간.
    public float CookRemaining
    {
        get
        {
            var best = float.MaxValue;
            for (var lane = 0; lane < Lanes; lane++)
                if (State(lane).Value == StationState.Cooking)
                    best = Mathf.Min(best, RemainingOf(lane));

            return best == float.MaxValue ? 0f : best;
        }
    }

    float RemainingOf(int lane) =>
        Mathf.Max(0f, (float)(DoneAt(lane).Value - NetworkManager.ServerTime.Time));

    public string Prompt
    {
        get
        {
            // 집어 갈 것이 있으면 그것이 먼저다. F가 무엇을 할지와 같은 순서로 읽힌다.
            if (LaneWith(StationState.Product) >= 0) return $"{name} · 완성품 가져가기";

            for (var lane = 0; lane < Lanes; lane++)
            {
                if (State(lane).Value == StationState.Gauge)
                    return $"{name} · 완성 게이지 {gauge.Needle:0.00}";

                // 조리가 끝났는데 게이지가 다른 레인에 잡혀 있다.
                if (State(lane).Value == StationState.Cooking && RemainingOf(lane) <= 0f)
                    return $"{name} · 게이지 대기";
            }

            var cooking = LaneWith(StationState.Cooking);
            if (cooking >= 0) return $"{name} · 조리 {RemainingOf(cooking):0.0}s";

            var count = TotalLoaded;
            return count == 0 ? $"{name} · 재료 넣기" : $"{name} · {count}개 적재";
        }
    }

    int TotalLoaded
    {
        get
        {
            var n = 0;
            for (var lane = 0; lane < Lanes; lane++) n += Loaded(lane).Count;
            return n;
        }
    }

    public void BeginInteractionClient() => UseRpc();
    public void EndInteractionClient() { }

    /// 레인마다 적재 칸이 늘어선다. 완성되면 그 레인의 첫 칸을 완성품 하나가 대신 쓴다 —
    /// 재료는 이미 기계 안에서 사라졌고, 남은 것은 집어 갈 결과물 하나뿐이다.
    ///
    /// 앵커는 `MaxLanes × maxIngredients`만큼 있어야 2레인이 전부 보인다. 모자라면 뒤쪽
    /// 레인이 안 보일 뿐 기능은 돈다 (`ItemDisplay`).
    public event System.Action ContentsChanged;
    public int SlotCount => Lanes * maxIngredients;

    public CarryView SlotAt(int index)
    {
        if (index < 0 || index >= SlotCount) return CarryView.Nothing;

        var lane = index / maxIngredients;
        var slot = index % maxIngredients;

        if (State(lane).Value == StationState.Product)
            return slot == 0 ? ProductView(lane).Value : CarryView.Nothing;

        var items = Loaded(lane);
        return slot < items.Count ? CarryView.Of((Ingredient)items[slot]) : CarryView.Nothing;
    }

    public int HighlightSlot => -1;

    /// 지금 게이지를 잡고 있는 레인이 `Temp.Cold` 메뉴인가 (기획서 9.1 「얼음 장인」).
    ///
    /// 서버 전용이다 — `gaugeLane`이 서버 값이고, 판정도 서버가 한다
    /// (`CompletionGauge.JudgeFor`).
    public bool GaugeLaneIsCold
    {
        get
        {
            if (gaugeLane < 0 || gaugeLane >= MaxLanes) return false;

            var items = Loaded(gaugeLane);
            if (items.Count == 0) return false;

            var recipe = new Ingredient[items.Count];
            for (var i = 0; i < items.Count; i++) recipe[i] = (Ingredient)items[i];

            return (Menus.TagsOf(recipe) & MenuTag.Cold) != 0;
        }
    }

    public override void OnNetworkSpawn()
    {
        gauge = GetComponent<CompletionGauge>();
        if (IsServer) gauge.OnResult += OnJudged;

        loaded0.OnListChanged += OnLoadedChanged;
        loaded1.OnListChanged += OnLoadedChanged;
        state0.OnValueChanged += OnStateChanged;
        state1.OnValueChanged += OnStateChanged;
        productView0.OnValueChanged += OnProductChanged;
        productView1.OnValueChanged += OnProductChanged;

        if (Owner != null) Owner.UpgradesChanged += OnUpgradesChanged;

        ContentsChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && gauge != null) gauge.OnResult -= OnJudged;

        loaded0.OnListChanged -= OnLoadedChanged;
        loaded1.OnListChanged -= OnLoadedChanged;
        state0.OnValueChanged -= OnStateChanged;
        state1.OnValueChanged -= OnStateChanged;
        productView0.OnValueChanged -= OnProductChanged;
        productView1.OnValueChanged -= OnProductChanged;

        if (ownerCafe != null) ownerCafe.UpgradesChanged -= OnUpgradesChanged;
    }

    void OnLoadedChanged(NetworkListEvent<int> _) => ContentsChanged?.Invoke();
    void OnStateChanged(StationState _, StationState __) => ContentsChanged?.Invoke();
    void OnProductChanged(CarryView _, CarryView __) => ContentsChanged?.Invoke();

    /// 레인이 늘면 보여 줄 칸도 는다.
    void OnUpgradesChanged() => ContentsChanged?.Invoke();

    /// 이 레인에 게이지를 붙일 때가 됐는가. 조리는 낮에만 진행된다 (기획서 4장) — 밤에
    /// 걸어 두고 다음 낮에 완성품을 받는 경로를 막는다. 순수 판정이라 씬 없이 테스트한다.
    public static bool ShouldBeginGauge(Phase phase, StationState state, float remaining) =>
        phase == Phase.Day && state == StationState.Cooking && remaining <= 0f;

    /// 조리가 끝난 레인에 게이지를 붙인다. 게이지는 하나뿐이라 한 번에 한 레인만 잡는다
    /// (클래스 주석).
    void Update()
    {
        if (!IsServer || Director == null) return;
        if (gauge.Active) return;

        for (var lane = 0; lane < Lanes; lane++)
        {
            if (!ShouldBeginGauge(Director.Phase.Current, State(lane).Value, RemainingOf(lane)))
                continue;

            State(lane).Value = StationState.Gauge;
            gaugeLane = lane;
            gauge.BeginServer();
            return;
        }
    }

    /// 키 하나에 RPC 하나. F가 무엇을 뜻하는지는 설비와 플레이어가 들고 있는 것에 따라
    /// 달라지는데, 뒤쪽 절반은 서버만 알고 있다.
    [Rpc(SendTo.Server)]
    public void UseRpc(RpcParams p = default)
    {
        var clientId = p.Receive.SenderClientId;
        if (Director == null || Director.Phase.Current != Phase.Day) return;
        if (!InReach(clientId)) return;
        if (!Cafe.SameTeamServer(this, clientId)) return;     // 내 기계가 아니다

        // 멈춘 기계도 이미 만들어 둔 것은 돌려주지만 새로 받지는 않는다 (기획서 3.3).
        var done = LaneWith(StationState.Product);
        if (done >= 0) { TakeProduct(clientId, done); return; }
        if (disabled.Value) return;

        var carry = PlayerCarry.Of(clientId);
        if (carry == null) return;

        if (!carry.Empty)
        {
            var lane = LaneForInsert(carry.Held);
            if (lane >= 0) Insert(carry, lane);
            return;
        }

        var ready = LaneReadyToCook();
        if (ready >= 0) Cook(ready);
    }

    /// 그 상태인 첫 레인. 없으면 -1.
    int LaneWith(StationState want)
    {
        for (var lane = 0; lane < Lanes; lane++)
            if (State(lane).Value == want) return lane;
        return NoLane;
    }

    /// 재료가 들어갈 레인. 이미 담고 있는 레인이 우선이다 — 한 잔을 이어서 만드는 것이
    /// 기본이고, 빈 레인부터 채우면 두 잔이 반씩 만들어진 채로 남는다.
    int LaneForInsert(HeldItem held)
    {
        if (held.IsProduct) return NoLane;
        if (held.IsAssembly) return LaneForAssembly(held);

        for (var lane = 0; lane < Lanes; lane++)
            if (Accepts(lane, held.Ingredient) && Loaded(lane).Count > 0) return lane;

        for (var lane = 0; lane < Lanes; lane++)
            if (Accepts(lane, held.Ingredient)) return lane;

        return NoLane;
    }

    /// 조리대에서 조립해 온 것이 들어갈 레인 (기획서 5.1). 조립물은 한 판 전체라 절반쯤
    /// 채워진 레인에 섞이지 않는다 — 빈 레인에만 통째로 들어간다.
    int LaneForAssembly(HeldItem held)
    {
        if (!AcceptsAssembly || held.Recipe.Length > maxIngredients) return NoLane;

        for (var lane = 0; lane < Lanes; lane++)
            if (State(lane).Value == StationState.Idle && Loaded(lane).Count == 0) return lane;

        return NoLane;
    }

    bool Accepts(int lane, Ingredient ingredient)
    {
        if (State(lane).Value != StationState.Idle) return false;

        var count = Loaded(lane).Count;
        return count < maxIngredients && AcceptsIngredient(ingredient, count);
    }

    /// 조리를 시작할 레인. 재료가 든 Idle 레인 중 첫 번째다.
    int LaneReadyToCook()
    {
        for (var lane = 0; lane < Lanes; lane++)
            if (State(lane).Value == StationState.Idle && Loaded(lane).Count > 0) return lane;
        return NoLane;
    }

    /// 첫 재료가 그릇 하나를 점유한다. 깨끗한 그릇이 없으면 주문을 시작할 수 없다 (기획서 5.3).
    void Insert(PlayerCarry carry, int lane)
    {
        var items = Loaded(lane);
        if (items.Count == 0)
        {
            var dishes = Owner != null ? Owner.Dishes : null;
            if (dishes == null || !dishes.ClaimServer()) return;
        }

        // 조립물은 통째로 들어간다. 재료를 하나씩 옮겨 담으면 조리대에서 맞춰 둔 조합이
        // 레인 용량에 걸려 반만 들어갈 수 있다.
        var held = carry.Held;
        if (held.IsAssembly)
        {
            for (var i = 0; i < held.Recipe.Length; i++) items.Add((int)held.Recipe[i]);
            carry.ClearServer();
            return;
        }

        items.Add((int)held.Ingredient);

        // 「양손잡이」가 둘을 들고 있어도 하나만 들어간다 (`PlayerCarry.ConsumeOneServer`).
        carry.ConsumeOneServer();
    }

    void Cook(int lane)
    {
        if (!CanCook()) return;

        State(lane).Value = StationState.Cooking;

        // 임대료 페널티 1단계부터 모든 제작 시간이 늘어난다 (기획서 3.3 낮 항목).
        var ledger = Director != null && Owner != null ? Director.LedgerOf(Owner.TeamId) : null;
        var scale = ledger != null ? ledger.CraftSpeedScale : 1f;
        DoneAt(lane).Value = NetworkManager.ServerTime.Time + cookSeconds * scale;
    }

    protected virtual bool AcceptsIngredient(Ingredient ingredient, int currentCount) => true;

    /// 조리대에서 조립해 온 것을 받는가 (기획서 5.1). 오븐만 받는다 — 커피 머신은 재료를
    /// 직접 받는 것이 기획서의 커피 흐름이다.
    protected virtual bool AcceptsAssembly => false;

    protected virtual bool CanCook() => true;

    void TakeProduct(ulong clientId, int lane)
    {
        var carry = PlayerCarry.Of(clientId);
        if (carry == null || !carry.Empty) return;

        carry.SetServer(products[lane]);
        SetProductServer(lane, HeldItem.Nothing);
        State(lane).Value = StationState.Idle;
    }

    /// 완성품은 규칙용 원본과 표시용 사본 두 벌이다. 한 곳에서만 바꿔서 둘이 갈라지지
    /// 않게 한다 — 갈라지면 기계 위에 있지도 않은 것이 보인다.
    void SetProductServer(int lane, HeldItem item)
    {
        products[lane] = item;
        ProductView(lane).Value = CarryView.Of(item);
    }

    void OnJudged(Judgement j)
    {
        // 게이지를 잡아 둔 레인의 결과다. 게이지가 하나뿐이라 어느 레인인지는 이 값으로만
        // 알 수 있다 — 상태로 찾으면 두 레인이 동시에 Gauge일 때 틀린다.
        var lane = gaugeLane;
        gaugeLane = NoLane;
        if (lane < 0 || lane >= MaxLanes) return;

        var items = Loaded(lane);
        var recipe = new Ingredient[items.Count];
        for (var i = 0; i < items.Count; i++) recipe[i] = (Ingredient)items[i];
        items.Clear();

        SetProductServer(lane, new HeldItem
        {
            IsProduct = true,
            Recipe = recipe,
            Menu = Menus.Match(recipe),
            GaugeMultiplier = CompletionGauge.MultiplierOf(j),
            Burnt = j == Judgement.Burnt,
        });
        State(lane).Value = StationState.Product;

        // 여기서는 "그 손님"을 알 수 없다. 음료는 서빙되기 전까지 주인이 없다.
        // ponytail: 대기열 맨 앞 손님을 회복시킨다. 주문이 미리 설비에 배정되는 방식으로
        // 바뀌면 다시 본다.
        if (j == Judgement.Perfect && Owner != null && Owner.Queue != null)
            Owner.Queue.RestoreFrontServer();
    }

    bool InReach(ulong clientId)
    {
        var t = PlayerOf(clientId);
        return t != null && WithinReach(surface, transform, t.position, reach);
    }

    public static Transform PlayerOf(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.ConnectedClients.TryGetValue(clientId, out var c)) return null;
        return c.PlayerObject != null ? c.PlayerObject.transform : null;
    }

    /// 낮 쪽 설비가 공통으로 쓴다. Day/는 Night/PlayerInteract를 재사용할 수 없다.
    public static bool LocalPlayerNear(Collider surface, Transform t, float reach)
    {
        var nm = NetworkManager.Singleton;
        var po = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
        return po != null && WithinReach(surface, t, po.transform.position, reach);
    }

    /// 설비까지의 거리는 원점이 아니라 **콜라이더 표면**에서 잰다.
    ///
    /// 상호작용 후보는 콜라이더가 겹치면 잡히는데(`PlayerInteractor`의 트리거) 서버 판정만
    /// 원점 기준이면 둘이 어긋난다. 조리대(12×1.6)와 카운터(8×1)는 원점이 한가운데라 끝에
    /// 서면 **프롬프트는 뜨는데 F가 통째로 무시됐다** — 12m 조리대에서 실제로 쓸 수 있는
    /// 구간이 가운데 2.5m뿐이었다. 머신·싱크는 1.5×1.5라 이 차이가 드러나지 않았다.
    ///
    /// 콜라이더 안에 서 있으면 `ClosestPoint`가 그 점을 그대로 돌려주므로 거리는 0이다.
    /// 볼록하지 않은 MeshCollider는 `ClosestPoint`가 지원하지 않아 원점으로 되돌아간다.
    public static bool WithinReach(Collider surface, Transform fixture, Vector3 point, float reach)
    {
        var mesh = surface as MeshCollider;
        if (surface == null || (mesh != null && !mesh.convex))
            return Vector3.Distance(fixture.position, point) <= reach;

        return Vector3.Distance(surface.ClosestPoint(point), point) <= reach;
    }
}
