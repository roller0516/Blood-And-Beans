using Unity.Netcode;
using UnityEngine;

/// 조리대 (기획서 5.4). 제조존과 보급·세척존을 가르는 통과 불가 섬이고, **그 너머로
/// 아이템을 건넬 수 있다** (5.4-2).
///
/// 서빙 카운터는 위쪽에만 붙어 있어서 아래에 있는 사람은 서빙할 수 없다 (5.4-3).
/// 그래서 아래에서 꺼낸 재료를 위로 넘기는 이 통로가 곧 분업의 조건이다. 좌우 끝으로
/// 돌아가는 길만 있으면 둘이 나눠 설 이유가 사라진다.
///
/// **기본은 한 칸짜리다.** 올려 둔 것을 누가 집어 가기 전에는 다음 것을 올릴 수 없다.
/// 큐를 두면 조리대가 창고가 되어 동선을 길게 만든 레이아웃 설계가 무의미해진다.
///
/// 「전달 벨트」를 설치하면 칸이 둘이 된다 (기획서 8장: "조리대에 올린 재료가 반대편으로
/// 자동 이동"). 올린 것은 넘김 시간이 지나면 스스로 반대편 칸으로 흘러가고, 그 사이에
/// 올린 쪽 칸이 비어 다음 것을 올릴 수 있다. 받는 쪽은 흘러온 것부터 집는다 — 그것이
/// 이 업그레이드가 동선에서 지워 주는 왕복이다.
///
/// **디저트는 여기서 조립한다** (기획서 5.1: 빵 베이스를 올리고 크림을 얹은 뒤 오븐에
/// 넣는다). 바탕이 올라와 있을 때 얹을 수 있는 재료를 들고 누르면 올려놓는 대신 그 위에
/// 얹힌다. 오븐은 낱개 재료를 받지 않으므로(`Oven`) 이 조립이 디저트의 유일한 경로이고,
/// 그래서 디저트가 커피보다 단계가 하나 많다.
public class PrepIsland : NetworkBehaviour, IInteractable, IItemHolder
{
    [SerializeField] float reach = 2.5f;

    /// 「전달 벨트」가 올린 것을 반대편으로 넘기는 데 걸리는 시간 (기획서 8장).
    /// ponytail: 기획서 8장에 수치가 없고 14장에도 항목이 없다. 걸어서 돌아가는 것보다는
    /// 빨라야 업그레이드가 의미를 갖는다. 표가 생기면 옮긴다.
    [SerializeField] float beltSeconds = 1.2f;

    /// 서버 권위 내용물. 규칙에 필요한 것은 전부 여기 있고, 화면에 필요한 것만 복제된다.
    /// 0번은 올린 쪽 칸, 1번은 벨트가 넘겨 준 반대편 칸이다.
    HeldItem placed = HeldItem.Nothing;
    HeldItem delivered = HeldItem.Nothing;

    /// 표시용. 조리대는 양쪽에서 보이므로 무엇이 올라와 있는지가 전원에게 복제돼야 한다 —
    /// 안 보이면 건네주기가 성립하지 않는다.
    readonly NetworkVariable<CarryView> view = new(CarryView.Nothing);
    readonly NetworkVariable<CarryView> deliveredView = new(CarryView.Nothing);

    /// 올린 것이 반대편으로 넘어가는 서버 시각. 벨트가 없으면 쓰이지 않는다.
    double beltArrivesAt;

    Cafe ownerCafe;

    /// 조립 루트는 전역이 아니라 소속 카페에서 받는다. 설비마다 따로 찾으면 카페별로
    /// 다른 답이 나올 여지가 생긴다 (아키텍처_v1.0.md §1.4).
    MatchDirector Director =>
        (ownerCafe != null ? ownerCafe : (ownerCafe = Cafe.Of(this)))?.Director;

    /// 벨트가 설치돼 있는가 (기획서 8장). 카페에서 읽는다 — 설비는 자기 카페의 상태를 본다.
    bool HasBelt =>
        (ownerCafe != null ? ownerCafe : (ownerCafe = Cafe.Of(this)))?.HasUpgrade(
            UpgradeId.ConveyorBelt) ?? false;

    /// 집을 때 먼저 나가는 칸. 벨트가 넘겨 준 것이 있으면 그것부터다 — 먼저 올린 것이
    /// 먼저 나가야 조리대가 순서를 뒤집지 않는다.
    CarryView Front => deliveredView.Value.Empty ? view.Value : deliveredView.Value;

    public string Prompt =>
        Front.Empty ? "조리대 · 올려두기" : $"조리대 · {Front.Label} 집기";

    /// 기본은 한 칸, 벨트를 달면 두 칸이다 (클래스 주석).
    public event System.Action ContentsChanged;
    public int SlotCount => HasBelt ? 2 : 1;

    public CarryView SlotAt(int index) => index switch
    {
        0 => view.Value,
        1 => HasBelt ? deliveredView.Value : CarryView.Nothing,
        _ => CarryView.Nothing,
    };

    public int HighlightSlot => -1;

    public override void OnNetworkSpawn()
    {
        view.OnValueChanged += OnViewChanged;
        deliveredView.OnValueChanged += OnViewChanged;

        ownerCafe = Cafe.Of(this);
        if (ownerCafe != null) ownerCafe.UpgradesChanged += OnUpgradesChanged;

        ContentsChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        view.OnValueChanged -= OnViewChanged;
        deliveredView.OnValueChanged -= OnViewChanged;
        if (ownerCafe != null) ownerCafe.UpgradesChanged -= OnUpgradesChanged;
    }

    void OnViewChanged(CarryView _, CarryView __) => ContentsChanged?.Invoke();

    /// 벨트를 떼는 일은 없지만(효과는 그 판 동안 영구다), 칸 수가 바뀌면 표시를 다시
    /// 그려야 한다.
    void OnUpgradesChanged()
    {
        // 벨트가 붙는 순간 이미 올라와 있던 것이 영영 넘어가지 않으면 안 된다.
        if (IsServer && HasBelt && !placed.Empty && delivered.Empty)
            beltArrivesAt = NetworkManager.ServerTime.Time + beltSeconds;

        ContentsChanged?.Invoke();
    }

    /// 벨트가 올린 것을 반대편으로 넘긴다. 반대편이 비어 있을 때만 흐른다 — 두 칸이
    /// 다 차면 창고가 되고, 그것이 한 칸짜리로 둔 이유였다 (클래스 주석).
    void Update()
    {
        if (!IsServer || !HasBelt) return;
        if (placed.Empty || !delivered.Empty) return;
        if (NetworkManager.ServerTime.Time < beltArrivesAt) return;

        delivered = placed;
        placed = HeldItem.Nothing;
        deliveredView.Value = CarryView.Of(delivered);
        view.Value = CarryView.Nothing;
    }

    public void BeginInteractionClient() => UseRpc();
    public void EndInteractionClient() { }

    /// 손에 든 것이 있으면 올려놓고, 비어 있으면 올려진 것을 집는다. 키 하나가 무엇을
    /// 뜻하는지는 서버가 아는 상태(누구 손에 무엇이 있는가)로 갈린다 — `Station.UseRpc`와
    /// 같은 방식이다.
    [Rpc(SendTo.Server)]
    public void UseRpc(RpcParams p = default)
    {
        var clientId = p.Receive.SenderClientId;
        if (Director == null || Director.Phase.Current != Phase.Day) return;
        if (!InReach(clientId)) return;

        // 손이 닿는다고 권한이 있는 것은 아니다. 상대 카페로 걸어 들어가 조리대의 완성품을
        // 집어 갈 수 있으면 안 된다 (`Cafe.SameTeamServer` 주석).
        if (!Cafe.SameTeamServer(this, clientId)) return;

        var carry = PlayerCarry.Of(clientId);
        if (carry == null) return;

        // 얹기가 먼저다. 올려놓기보다 뒤에 두면 바탕이 있는 칸은 늘 "자리 참"으로 걸려
        // 조립이 시작조차 하지 못한다.
        if (!carry.Empty && CanTop(carry.Held)) Top(carry);
        else if (!carry.Empty && placed.Empty) Place(carry);
        else if (carry.Empty && !(placed.Empty && delivered.Empty)) Take(carry);
    }

    /// 올라와 있는 바탕 위에 이 손을 얹을 수 있는가 (기획서 5.1).
    ///
    /// 상한은 메뉴 표가 정한다 (`Menus.MaxDessertParts`). 없으면 한 덩어리에 재료를 계속
    /// 얹을 수 있고, 그렇게 만든 것은 오븐 한 레인에 들어가지 못해 낮이 끝날 때까지
    /// 손에 남는다.
    ///
    /// ponytail: 얹기는 올린 쪽 칸에서만 된다. 벨트가 반대편으로 넘긴 뒤에는 받는 쪽이
    /// 집어서 다시 올려야 얹을 수 있다 — 두 칸 모두에 얹기를 열면 같은 조립물이 양쪽에서
    /// 자라는 경우를 따로 막아야 한다. 벨트를 단 팀의 디저트 동선이 실제로 불편하면 그때 연다.
    bool CanTop(HeldItem held) =>
        !placed.Empty && !placed.IsProduct && placed.Ingredient == Menus.DessertBase &&
        !held.IsProduct && !held.IsAssembly && Menus.IsDessertTopping(held.Ingredient) &&
        PartsOf(placed).Length < Menus.MaxDessertParts;

    void Top(PlayerCarry carry)
    {
        var parts = PartsOf(placed);
        var next = new Ingredient[parts.Length + 1];
        System.Array.Copy(parts, next, parts.Length);
        next[parts.Length] = carry.Held.Ingredient;

        placed = HeldItem.Assembled(next);
        carry.ConsumeOneServer();
        view.Value = CarryView.Of(placed);
    }

    /// 아직 아무것도 얹지 않은 바탕은 재료 하나짜리 조립물과 같다.
    static Ingredient[] PartsOf(HeldItem item) =>
        item.IsAssembly ? item.Recipe : new[] { item.Ingredient };

    void Place(PlayerCarry carry)
    {
        placed = carry.Held;
        carry.ClearServer();
        view.Value = CarryView.Of(placed);

        // 벨트가 넘기기 시작하는 시각. 벨트가 없으면 이 값은 읽히지 않는다.
        beltArrivesAt = NetworkManager.ServerTime.Time + beltSeconds;
    }

    /// 넘어온 칸부터 비운다. 먼저 올린 것이 먼저 나가야 조리대가 순서를 뒤집지 않는다.
    void Take(PlayerCarry carry)
    {
        if (!delivered.Empty)
        {
            carry.SetServer(delivered);
            delivered = HeldItem.Nothing;
            deliveredView.Value = CarryView.Nothing;
            return;
        }

        carry.SetServer(placed);
        placed = HeldItem.Nothing;
        view.Value = CarryView.Nothing;
    }

    bool InReach(ulong clientId)
    {
        var t = Station.PlayerOf(clientId);
        return t != null && Station.WithinReach(surface, transform, t.position, reach);
    }

    /// 손이 닿는지 재는 기준면. 조리대는 12m짜리 한 덩어리라 원점에서 재면 가운데 몇
    /// 미터만 살아난다 (`Station.WithinReach`).
    Collider surface;

    void Awake() => surface = GetComponentInChildren<Collider>(true);
}
