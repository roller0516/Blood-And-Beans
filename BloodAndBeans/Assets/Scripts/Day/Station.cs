using Unity.Netcode;
using UnityEngine;

public enum StationState { Idle, Cooking, Gauge, Product }

/// 재료 투입 -> 자동 진행 -> 완성 게이지 (기획서 5.1).
/// 커피 머신과 오븐은 제작 시간만 다르므로 나머지는 전부 여기 있다.
[RequireComponent(typeof(CompletionGauge))]
public class Station : NetworkBehaviour, IInteractable
{
    [SerializeField] protected float cookSeconds = 5f;

    readonly NetworkVariable<bool> disabled = new();

    public bool Disabled => disabled.Value;

    /// 임대료 페널티 2단계부터 그날 하루 기계 한 대가 멈춘다 (기획서 3.3).
    public void SetDisabledServer(bool value)
    {
        if (!IsServer) return;
        disabled.Value = value;
    }
    [SerializeField] float reach = 2.5f;
    [SerializeField] int maxIngredients = 4;

    readonly NetworkList<int> loaded = new();
    readonly NetworkVariable<StationState> state = new();
    readonly NetworkVariable<double> doneAt = new();

    CompletionGauge gauge;
    Cafe ownerCafe;

    /// 조립 루트는 전역이 아니라 소속 카페에서 받는다. 설비마다 따로 찾으면 카페별로
    /// 다른 답이 나올 여지가 생긴다 (아키텍처_v1.0.md §1.4).
    MatchDirector Director =>
        (ownerCafe != null ? ownerCafe : (ownerCafe = Cafe.Of(this)))?.Director;
    HeldItem product;      // 서버 측. 누군가 집어 가기를 기다리는 완성품

    public StationState State => state.Value;
    public float Reach => reach;
    public int LoadedCount => loaded.Count;
    public float CookRemaining =>
        Mathf.Max(0f, (float)(doneAt.Value - NetworkManager.ServerTime.Time));
    public string Prompt => state.Value switch
    {
        StationState.Cooking => $"{name} · 조리 {CookRemaining:0.0}s",
        StationState.Gauge => $"{name} · 완성 게이지 {gauge.Needle:0.00}",
        StationState.Product => $"{name} · 완성품 가져가기",
        _ => loaded.Count == 0 ? $"{name} · 재료 넣기" : $"{name} · {loaded.Count}개 적재",
    };

    public void BeginInteractionClient() => UseRpc();
    public void EndInteractionClient() { }

    public override void OnNetworkSpawn()
    {
        gauge = GetComponent<CompletionGauge>();
        if (IsServer) gauge.OnResult += OnJudged;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && gauge != null) gauge.OnResult -= OnJudged;
    }

    void Update()
    {
        if (IsServer && Director != null &&
            ShouldBeginGauge(Director.Phase.Current, state.Value, CookRemaining))
        {
            state.Value = StationState.Gauge;
            gauge.BeginServer();
        }

    }

    public static bool ShouldBeginGauge(Phase phase, StationState stationState, float remaining) =>
        phase == Phase.Day && stationState == StationState.Cooking && remaining <= 0f;

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
        if (state.Value == StationState.Product) { TakeProduct(clientId); return; }
        if (disabled.Value) return;
        if (state.Value != StationState.Idle) return;

        var carry = PlayerCarry.Of(clientId);
        if (carry == null) return;

        if (!carry.Empty) Insert(carry);
        else if (loaded.Count > 0) Cook();
    }

    /// 첫 재료가 그릇 하나를 점유한다. 깨끗한 그릇이 없으면 주문을 시작할 수 없다 (기획서 5.3).
    void Insert(PlayerCarry carry)
    {
        var held = carry.Held;
        if (held.IsProduct || loaded.Count >= maxIngredients ||
            !AcceptsIngredient(held.Ingredient, loaded.Count)) return;
        var dishes = Cafe.Of(this)?.Dishes;
        if (loaded.Count == 0 && (dishes == null || !dishes.ClaimServer())) return;

        loaded.Add((int)held.Ingredient);
        carry.ClearServer();
    }

    void Cook()
    {
        if (!CanCook()) return;
        state.Value = StationState.Cooking;
        // 임대료 페널티 1단계부터 모든 제작 시간이 늘어난다 (기획서 3.3 낮 항목).
        var ledger = Director != null ? Director.LedgerOf(Cafe.Of(this)?.TeamId ?? -1) : null;
        var scale = ledger != null ? ledger.CraftSpeedScale : 1f;
        doneAt.Value = NetworkManager.ServerTime.Time + cookSeconds * scale;
    }

    protected virtual bool AcceptsIngredient(Ingredient ingredient, int currentCount) => true;
    protected virtual bool CanCook() => true;

    void TakeProduct(ulong clientId)
    {
        var carry = PlayerCarry.Of(clientId);
        if (carry == null || !carry.Empty) return;

        carry.SetServer(product);
        product = HeldItem.Nothing;
        state.Value = StationState.Idle;
    }

    void OnJudged(Judgement j)
    {
        var recipe = new Ingredient[loaded.Count];
        for (int i = 0; i < loaded.Count; i++) recipe[i] = (Ingredient)loaded[i];
        loaded.Clear();

        product = new HeldItem
        {
            IsProduct = true,
            Recipe = recipe,
            Menu = Menus.Match(recipe),
            GaugeMultiplier = CompletionGauge.MultiplierOf(j),
            Burnt = j == Judgement.Burnt,
        };
        state.Value = StationState.Product;

        // 여기서는 "그 손님"을 알 수 없다. 음료는 서빙되기 전까지 주인이 없다.
        // ponytail: 대기열 맨 앞 손님을 회복시킨다. 주문이 미리 설비에 배정되는 방식으로
        // 바뀌면 다시 본다.
        if (j == Judgement.Perfect) Cafe.Of(this)?.Queue?.RestoreFrontServer();
    }

    bool InReach(ulong clientId)
    {
        var t = PlayerOf(clientId);
        return t != null && Vector3.Distance(t.position, transform.position) <= reach;
    }

    public static Transform PlayerOf(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.ConnectedClients.TryGetValue(clientId, out var c)) return null;
        return c.PlayerObject != null ? c.PlayerObject.transform : null;
    }

    /// 낮 쪽 설비가 공통으로 쓴다. Day/는 Night/PlayerInteract를 재사용할 수 없다.
    public static bool LocalPlayerNear(Transform t, float reach)
    {
        var nm = NetworkManager.Singleton;
        var po = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
        return po != null && Vector3.Distance(po.transform.position, t.position) <= reach;
    }

}
