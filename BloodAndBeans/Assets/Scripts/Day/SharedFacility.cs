using Unity.Netcode;
using UnityEngine;

public enum FacilityKind { Coffee, Oven, Sink, Beans, Bread }

/// 광장은 점유 여부만 공개한다. 조리·게이지는 이용 팀 카페의 비공개 Station이 소유한다.
public sealed class SharedFacility : NetworkBehaviour, IInteractable
{
    [SerializeField] FacilityKind kind;
    [SerializeField] float reach = 2.5f;
    [SerializeField] Renderer statusRenderer;
    readonly NetworkVariable<bool> busy = new();
    readonly NetworkVariable<int> occupyingTeam = new(-1);
    ulong user;
    PlayerCarry washing;
    double completesAt;
    Collider surface;
    MatchDirector director;
    public FacilityKind Kind => kind;
    public bool Busy => busy.Value;
    public int OccupyingTeam => occupyingTeam.Value;
    public string Prompt => $"{FacilityName} · {(Busy ? "사용 중" : kind == FacilityKind.Sink ? "F 홀드 세척" : "F 사용")}";
    string FacilityName => kind switch { FacilityKind.Coffee => "커피 머신", FacilityKind.Oven => "오븐",
        FacilityKind.Sink => "개수대", FacilityKind.Beans => "원두함", _ => "빵함" };
    void Awake()
    {
        surface = GetComponent<Collider>();
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
        if (director == null || director.Phase.Current != Phase.Day || Busy || !Near(id)) return;
        var cafe = director.CafeOf(PlayerTeam.Of(id));
        var carry = PlayerCarry.Of(id);
        if (cafe == null || carry == null || carry.Reserved) return;
        if (kind == FacilityKind.Beans || kind == FacilityKind.Bread)
        {
            var plate = kind == FacilityKind.Bread;
            if (!carry.Held.HasDish || carry.Held.IsProduct || carry.Held.Dirty || carry.Held.Ingredient != Ingredient.None || carry.Held.DishIsPlate != plate) return;
            carry.SetServer(HeldItem.Of(kind == FacilityKind.Beans ? Ingredient.Bean : Ingredient.BreadBase));
            carry.SetDishServer(true, kind == FacilityKind.Bread);
            return;
        }
        if (kind == FacilityKind.Sink)
        {
            if (carry.Held.HasDish && !carry.Held.Dirty && (carry.Held.IsProduct || carry.Held.Ingredient != Ingredient.None))
                carry.SetServer(HeldItem.Dish(carry.Held.DishIsPlate, true));
            if (carry.Empty && cafe.Dishes.TakeDirtyServer(out var dirtyPlate))
                carry.SetServer(HeldItem.Dish(dirtyPlate, true));
            if (!carry.Held.Dirty) return;
            occupyingTeam.Value = cafe.TeamId;
            busy.Value = true;
            user = id;
            washing = carry;
            carry.ReserveServer(true);
            var scale = cafe.HasBuff(TeamBuff.Wash) ? DayBalance.BuffSpeed : 1f;
            completesAt = NetworkManager.ServerTime.Time + DayBalance.WashSeconds *
                (PlayerCharacter.Of(id)?.WorkScale(3) ?? 1f) / scale;
            return;
        }
        foreach (var gauge in cafe.Gauges)
        {
            var station = gauge.Station;
            if (station == null || (kind == FacilityKind.Oven) != (station is Oven)) continue;
            if (!station.StartPublicServer(this, carry)) continue;
            occupyingTeam.Value = cafe.TeamId;
            busy.Value = true;
            user = id;
            return;
        }
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
    public void ReleaseServer()
    {
        if (!IsServer) return;
        if (washing != null) washing.ReserveServer(false);
        washing = null;
        busy.Value = false;
        occupyingTeam.Value = -1;
    }
}
