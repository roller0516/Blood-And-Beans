using Unity.Netcode;
using UnityEngine;

public enum StationState { Idle, Cooking, Gauge, Product }

/// Ingredients in -> runs by itself -> completion gauge (doc 5.1).
/// Coffee machine and oven differ only in cook time, so everything lives here.
[RequireComponent(typeof(CompletionGauge))]
public class Station : NetworkBehaviour, IInteractable
{
    [SerializeField] protected float cookSeconds = 5f;

    readonly NetworkVariable<bool> disabled = new();

    public bool Disabled => disabled.Value;

    /// Rent tier 2+ takes a machine out of service for the day (doc 3.3).
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
    MatchDirector director;
    HeldItem product;      // server-side, waiting to be picked up

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
        director = MatchDirector.Find();
        if (IsServer) gauge.OnResult += OnJudged;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && gauge != null) gauge.OnResult -= OnJudged;
    }

    void Update()
    {
        if (IsServer && director != null &&
            ShouldBeginGauge(director.Phase.Current, state.Value, CookRemaining))
        {
            state.Value = StationState.Gauge;
            gauge.BeginServer();
        }

    }

    public static bool ShouldBeginGauge(Phase phase, StationState stationState, float remaining) =>
        phase == Phase.Day && stationState == StationState.Cooking && remaining <= 0f;

    /// One key, one RPC. What F means depends on the station and on what the player
    /// is carrying, and only the server knows the second half.
    [Rpc(SendTo.Server)]
    public void UseRpc(RpcParams p = default)
    {
        var clientId = p.Receive.SenderClientId;
        if (director == null || director.Phase.Current != Phase.Day) return;
        if (!InReach(clientId)) return;
        if (!Cafe.SameTeamServer(this, clientId)) return;     // not your machine

        // A machine taken out of service still hands back whatever it already made,
        // but accepts nothing new (doc 3.3).
        if (state.Value == StationState.Product) { TakeProduct(clientId); return; }
        if (disabled.Value) return;
        if (state.Value != StationState.Idle) return;

        var carry = PlayerCarry.Of(clientId);
        if (carry == null) return;

        if (!carry.Empty) Insert(carry);
        else if (loaded.Count > 0) Cook();
    }

    /// First ingredient claims a dish — with none clean, no order can start (doc 5.3).
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
        // Rent tier 1+ stretches every cook (doc 3.3, day column).
        var ledger = director != null ? director.LedgerOf(Cafe.Of(this)?.TeamId ?? -1) : null;
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

        // "that customer" is not knowable here — the drink has no owner until it is
        // served. ponytail: restores the head of the queue; revisit if orders ever
        // get assigned to a station up front.
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

    /// Shared by every day-side prop — Day/ can't reuse Night/PlayerInteract.
    public static bool LocalPlayerNear(Transform t, float reach)
    {
        var nm = NetworkManager.Singleton;
        var po = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
        return po != null && Vector3.Distance(po.transform.position, t.position) <= reach;
    }

}
