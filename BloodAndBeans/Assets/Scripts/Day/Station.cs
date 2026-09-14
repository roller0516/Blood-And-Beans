using Unity.Netcode;
using UnityEngine;

public enum StationState { Idle, Cooking, Gauge, Product }

/// 팀 전용 조리 상태. 공개된 설비의 점유는 SharedFacility가 소유한다 (5.4.1).
[RequireComponent(typeof(CompletionGauge))]
public class Station : NetworkBehaviour, IItemHolder
{
    readonly NetworkVariable<bool> disabled = new();
    readonly NetworkVariable<StationState> state = new();
    readonly NetworkVariable<double> doneAt = new();
    readonly NetworkVariable<Ingredient> ingredient = new(Ingredient.None);
    readonly NetworkVariable<Vector3> facilityPosition = new();
    readonly NetworkVariable<ulong> operatorId = new(ulong.MaxValue);
    readonly NetworkVariable<float> cookDuration = new();
    SharedFacility facility;
    PlayerCarry operatorCarry;
    CompletionGauge gauge;
    Cafe cafe;
    HeldItem input;
    public bool Disabled => disabled.Value;
    public StationState State => state.Value;
    public ulong OperatorId => operatorId.Value;
    public float CookProgress => cookDuration.Value > 0f ? Mathf.Clamp01(1f - CookRemaining / cookDuration.Value) : 0f;
    public Vector3 FacilityPosition => facilityPosition.Value;
    public float CookRemaining => NetworkManager == null ? 0f : Mathf.Max(0f, (float)(doneAt.Value - NetworkManager.ServerTime.Time));
    public event System.Action ContentsChanged;
    public int SlotCount => 1;
    public int HighlightSlot => -1;
    public CarryView SlotAt(int slot) => slot == 0 ? CarryView.Of(ingredient.Value) : CarryView.Nothing;
    void Awake() { gauge = GetComponent<CompletionGauge>(); cafe = Cafe.Of(this); }
    public override void OnNetworkSpawn()
    {
        ingredient.OnValueChanged += OnIngredient;
        if (IsServer) gauge.OnResult += OnJudged;
    }
    public override void OnNetworkDespawn()
    {
        ingredient.OnValueChanged -= OnIngredient;
        if (IsServer) { gauge.OnResult -= OnJudged; if (facility != null) CancelServer(false); }
    }
    void OnIngredient(Ingredient _, Ingredient __) => ContentsChanged?.Invoke();
    public void SetDisabledServer(bool value) { if (IsServer) disabled.Value = value; }

    public bool StartPublicServer(SharedFacility source, PlayerCarry carry)
    {
        if (!IsServer || source == null || !source.IsSpawned || source.Busy || carry == null ||
            !carry.IsSpawned || carry.Reserved || Disabled || state.Value != StationState.Idle ||
            cafe == null || cafe.Director.Phase.Current != Phase.Day || PlayerTeam.Of(carry.OwnerClientId) != cafe.TeamId ||
            !source.Near(carry.OwnerClientId) || carry.Held.IsProduct || !carry.Held.HasDish) return false;
        var item = carry.Held;
        if (this is Oven ? source.Kind != FacilityKind.Oven || item.Ingredient != Ingredient.BreadBase || !item.DishIsPlate :
            source.Kind != FacilityKind.Coffee || item.DishIsPlate || (item.Ingredient != Ingredient.Bean && item.Ingredient != Ingredient.BloodBean)) return false;
        facility = source;
        facilityPosition.Value = source.transform.position;
        operatorCarry = carry;
        operatorId.Value = carry.OwnerClientId;
        input = item;
        ingredient.Value = item.Ingredient;
        carry.ClearServer();
        carry.ReserveServer(true);
        var seconds = this is Oven ? DayBalance.OvenSeconds : DayBalance.CoffeeSeconds;
        if (cafe.HasBuff(TeamBuff.Cook)) seconds /= DayBalance.BuffSpeed;
        cookDuration.Value = seconds * cafe.Director.LedgerOf(cafe.TeamId).CraftSpeedScale;
        doneAt.Value = NetworkManager.ServerTime.Time + cookDuration.Value;
        state.Value = StationState.Cooking;
        return true;
    }

    public static bool ShouldBeginGauge(Phase phase, StationState state, float remaining) =>
        phase == Phase.Day && state == StationState.Cooking && remaining <= 0f;

    void Update()
    {
        if (!IsServer || state.Value == StationState.Idle) return;
        var day = cafe != null && cafe.Director.Phase.Current == Phase.Day;
        if (!day || operatorCarry == null || !operatorCarry.IsSpawned || facility == null || !facility.IsSpawned)
        { CancelServer(false); return; }
        // ponytail: 조리 도중 이탈 규칙은 미결. 설비에서 벗어나면 투입물을 반환하고 취소한다.
        if (!facility.Near(operatorCarry.OwnerClientId)) { CancelServer(true); return; }
        if (ShouldBeginGauge(cafe.Director.Phase.Current, state.Value, CookRemaining))
        { state.Value = StationState.Gauge; gauge.BeginServer(); }
    }
    void CancelServer(bool returnInput)
    {
        if (returnInput && operatorCarry != null && operatorCarry.IsSpawned) operatorCarry.SetServer(input);
        else if (cafe != null && cafe.IsSpawned) cafe.Dishes.SoilServer(input.DishIsPlate);
        gauge.CancelServer();
        // 완성하지 않고 놓으면 과열도 없다. 방해만 하는 사용을 막는다 (5.4 · 9.1.2).
        ignited = false;
        ReleaseServer();
    }
    /// 「불붙이기」 (기획서 9.1.2). 이 플레이어가 조리 중인 설비일 때만 걸린다 — 먼저 써야 발동한다.
    public bool IgniteServer(ulong clientId)
    {
        if (!IsServer || state.Value != StationState.Cooking || operatorId.Value != clientId || ignited) return false;
        var remaining = CookRemaining;
        if (remaining <= 0f) return false;
        doneAt.Value -= remaining * DaySkills.IgniteCut;
        ignited = true;
        return true;
    }
    bool ignited;

    void ReleaseServer()
    {
        if (operatorCarry != null) operatorCarry.ReserveServer(false);
        facility?.ReleaseServer();
        // 놓은 뒤 달아오른다. 나와 팀원도 예외가 없다 (9.1.2).
        if (ignited && facility != null) facility.OverheatServer(DaySkills.OverheatSeconds);
        ignited = false;
        facility = null; operatorCarry = null;
        operatorId.Value = ulong.MaxValue;
        ingredient.Value = Ingredient.None;
        state.Value = StationState.Idle;
    }
    void OnJudged(Judgement judgement)
    {
        if (!IsServer || state.Value != StationState.Gauge) return;
        if (operatorCarry == null || !operatorCarry.IsSpawned || facility == null || !facility.Near(operatorCarry.OwnerClientId))
        { CancelServer(operatorCarry != null && operatorCarry.IsSpawned); return; }
        // 「정제」는 점유자의 다음 한 잔을 Perfect로 확정한다 (9.1.2). 탄 것은 한 잔으로 치지 않는다.
        if (judgement != Judgement.Burnt && PlayerCharacter.Of(operatorCarry.OwnerClientId)?.ConsumeRefineServer() == true)
            judgement = Judgement.Perfect;
        var recipe = new[] { input.Ingredient };
        operatorCarry.SetServer(new HeldItem { HasDish = true, DishIsPlate = input.DishIsPlate,
            IsProduct = true, Ingredient = Ingredient.None, Recipe = recipe, Menu = Menus.Match(recipe),
            GaugeMultiplier = CompletionGauge.MultiplierOf(judgement), Burnt = judgement == Judgement.Burnt });
        ReleaseServer();
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
