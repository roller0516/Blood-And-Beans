using Unity.Netcode;
using UnityEngine;

public enum FacilityKind { Coffee, Oven, Sink, Beans, Bread }

/// 광장 설비. 커피 머신·오븐은 같은 오브젝트의 Station이 조리·게이지를 맡는다 (5.4.1).
/// 점유 팀이 곧 그 Station의 이용 팀이다. 게이지는 화면에서만 점유 팀에게 보인다.
public sealed class SharedFacility : NetworkBehaviour, IInteractable
{
    [SerializeField] FacilityKind kind;
    [SerializeField] float reach = 2.5f;
    [SerializeField] Renderer statusRenderer;
    readonly NetworkVariable<bool> busy = new();
    readonly NetworkVariable<int> occupyingTeam = new(-1);

    /// 「불붙이기」가 남긴 과열이 끝나는 서버 시각. 그때까지 누구도 쓰지 못한다 (기획서 9.1.2).
    readonly NetworkVariable<double> hotUntil = new();
    ulong user;
    PlayerCarry washing;
    double completesAt;
    Collider surface;
    Station station;                             // 커피 머신·오븐에만 있다
    MatchDirector director;
    public FacilityKind Kind => kind;
    public bool Busy => busy.Value;
    public int OccupyingTeam => occupyingTeam.Value;
    public bool Hot => NetworkManager != null && IsSpawned && NetworkManager.ServerTime.Time < hotUntil.Value;
    public string Prompt => $"{FacilityName} · {(Busy ? "사용 중" : Hot ? "달아오름" : kind == FacilityKind.Sink ? "F 홀드 세척" : "F 사용")}";
    string FacilityName => kind switch { FacilityKind.Coffee => "커피 머신", FacilityKind.Oven => "오븐",
        FacilityKind.Sink => "개수대", FacilityKind.Beans => "원두함", _ => "빵함" };
    void Awake()
    {
        surface = GetComponent<Collider>();
        station = GetComponent<Station>();
        // 카페에서 복사한 모델의 팀 전용 레이어를 공용 설비 레이어로 맞춘다 (5.4.1).
        foreach (var child in GetComponentsInChildren<Transform>(true)) child.gameObject.layer = gameObject.layer;
    }
    public override void OnNetworkSpawn()
    {
        director = MatchDirector.Instance;
        busy.OnValueChanged += OnBusy;
        occupyingTeam.OnValueChanged += OnTeam;
        OnBusy(false, busy.Value);
    }
    public override void OnNetworkDespawn() { busy.OnValueChanged -= OnBusy; occupyingTeam.OnValueChanged -= OnTeam; }
    void OnTeam(int _, int __) => OnBusy(false, Busy);
    void OnBusy(bool _, bool value)
    {
        if (statusRenderer != null) TeamColors.TintWith(statusRenderer.gameObject, value ? TeamColors.Of(OccupyingTeam) : Color.white, 1f);
    }
    public void BeginInteractionClient() => UseRpc();
    public void EndInteractionClient() => EndRpc();
    public bool Near(ulong clientId)
    {
        var player = Station.PlayerOf(clientId);
        return player != null && Station.WithinReach(surface, transform, player.position, reach);
    }
    [Rpc(SendTo.Server)]
    public void UseRpc(RpcParams p = default)
    {
        var id = p.Receive.SenderClientId;
        if (director == null || director.Phase.Current != Phase.Day || Busy || Hot || !Near(id)) return;
        var cafe = director.CafeOf(PlayerTeam.Of(id));
        var carry = PlayerCarry.Of(id);
        if (cafe == null || carry == null || carry.Reserved) return;
        switch (kind)
        {
            case FacilityKind.Beans:
            case FacilityKind.Bread: GiveIngredientServer(carry); break;
            case FacilityKind.Sink: StartWashServer(id, cafe, carry); break;
            default: StartCookServer(id, cafe, carry); break;
        }
    }

    /// 손에 든 것으로 이 설비를 쓸 수 있는지 (기획서 5.7.4). 프롬프트와 서버가 같이 부른다.
    public static bool Accepts(FacilityKind kind, CarryView held)
    {
        if (!held.HasDish) return false;
        if (kind == FacilityKind.Sink) return held.Dirty || held.IsProduct || held.Ingredient != Ingredient.None;
        if (held.Dirty || held.IsProduct) return false;
        return kind switch
        {
            FacilityKind.Beans => !held.DishIsPlate && held.Ingredient == Ingredient.None,
            FacilityKind.Bread => held.DishIsPlate && held.Ingredient == Ingredient.None,
            FacilityKind.Coffee => !held.DishIsPlate && (held.Ingredient == Ingredient.Bean || held.Ingredient == Ingredient.BloodBean),
            FacilityKind.Oven => held.DishIsPlate && held.Ingredient == Ingredient.BreadBase,
            _ => false,
        };
    }

    /// 원두함·빵함이 내주는 재료.
    public static Ingredient Gives(FacilityKind kind) => kind == FacilityKind.Beans ? Ingredient.Bean : Ingredient.BreadBase;

    void GiveIngredientServer(PlayerCarry carry)
    {
        if (!IsServer || !Accepts(kind, CarryView.Of(carry.Held))) return;
        carry.SetServer(HeldItem.Of(Gives(kind)));
        carry.SetDishServer(true, kind == FacilityKind.Bread);
    }

    void StartWashServer(ulong id, Cafe cafe, PlayerCarry carry)
    {
        if (!IsServer) return;
        var view = CarryView.Of(carry.Held);
        if (view.Empty)
        {
            if (!cafe.Dishes.TakeDirtyServer(out var dirtyPlate)) return;
            carry.SetServer(HeldItem.Dish(dirtyPlate, true));
        }
        else if (!Accepts(FacilityKind.Sink, view)) return;
        else if (!view.Dirty) carry.SetServer(HeldItem.Dish(view.DishIsPlate, true)); // 내용물은 버리고 더러운 그릇만 남긴다.
        occupyingTeam.Value = cafe.TeamId;
        busy.Value = true;
        user = id;
        washing = carry;
        carry.ReserveServer(true);
        var scale = cafe.HasGem(Gem.Foam) ? Gems.WashTimeScale : 1f;
        completesAt = NetworkManager.ServerTime.Time + DayBalance.WashSeconds *
            (1f - Mathf.Clamp01(carry.Held.WashProgress)) * scale;
    }

    void StartCookServer(ulong id, Cafe cafe, PlayerCarry carry)
    {
        if (!IsServer || station == null) return;
        // Station은 점유 팀으로 카페를 푼다. 시작 전에 박고, 거절되면 되돌린다.
        occupyingTeam.Value = cafe.TeamId;
        if (!station.StartPublicServer(this, carry)) { occupyingTeam.Value = -1; return; }
        busy.Value = true;
        user = id;
    }
    [Rpc(SendTo.Server)]
    void EndRpc(RpcParams p = default)
    {
        if (kind == FacilityKind.Sink && busy.Value && user == p.Receive.SenderClientId) ReleaseServer();
    }
    void Update()
    {
        if (!IsServer || !busy.Value || kind != FacilityKind.Sink) return;
        if (washing == null || !washing.IsSpawned || director == null || director.Phase.Current != Phase.Day || !Near(user))
        { ReleaseServer(); return; }
        if (NetworkManager.ServerTime.Time < completesAt) return;
        washing.SetServer(HeldItem.Dish(washing.Held.DishIsPlate));
        ReleaseServer();
    }
    public void OverheatServer(float seconds)
    {
        if (IsServer) hotUntil.Value = NetworkManager.ServerTime.Time + seconds;
    }

    public void ReleaseServer()
    {
        if (!IsServer) return;
        if (washing != null) washing.ReserveServer(false);
        washing = null;
        busy.Value = false;
        occupyingTeam.Value = -1;
    }
}
