using Unity.Netcode;
using UnityEngine;

/// 재료 칸 (기획서 5.1, 5.4). 낮 루프의 진입점이다.
///
/// 재고는 더 이상 무한하지 않다. 원두와 빵 베이스만 상비이고(기획서 7.1), 나머지는 밤에
/// 캐서 TeamStock에 들어온 것만 꺼낼 수 있다 — 이것이 밤과 낮을 잇는 고리다(기획서 2장).
public class IngredientShelf : NetworkBehaviour, IInteractable, IItemHolder, ILootGrid
{
    /// 이 선반이 내줄 수 있는 재료를 순환 순서대로 나열한다. 실제로 재고가 있는지는
    /// 별개의 문제이고 그 답은 팀 재고가 한다.
    [SerializeField] Ingredient[] offer =
    {
        Ingredient.Bean, Ingredient.BreadBase, Ingredient.Milk,
        Ingredient.Cream, Ingredient.Chocolate, Ingredient.Almond,
        Ingredient.Berry, Ingredient.Ice,

        // 3등급 박스에서만 나오는 중심부 보상 (기획서 6.3). 여기 없으면 밤에 캐 와도
        // 꺼낼 수가 없어 팀 재고에 영원히 잠긴다.
        Ingredient.BloodBean, Ingredient.UpgradePart,
    };
    [SerializeField] float reach = 2.5f;

    /// 이 칸이 「재료 선반」 업그레이드로 제조존에 생기는 것인가 (기획서 8장: "특정 재료
    /// 1종이 제조존에도 비치된다"). 보급존의 본체는 false다.
    ///
    /// 설치 전에는 상호작용도 표시도 없다. 오브젝트째 꺼 두지 않는 이유는 이것이
    /// `NetworkBehaviour`이고 카페 루트의 `NetworkObject`에 매달려 있기 때문이다 —
    /// 비활성 상태로 스폰되면 NGO가 그 컴포넌트를 복제 대상에서 제외한다.
    [SerializeField] bool needsShelfUpgrade;

    int index = -1;          // 첫 입력이 offer[0]에 오도록 -1에서 시작한다
    TeamStock stock;
    Cafe ownerCafe;

    /// 설치 전에 감출 겉모습. 주기 실행 밖에서 한 번만 모은다.
    Renderer[] visuals = System.Array.Empty<Renderer>();

    /// 조립 루트는 전역이 아니라 소속 카페에서 받는다. 설비마다 따로 찾으면 카페별로
    /// 다른 답이 나올 여지가 생긴다 (아키텍처_v1.0.md §1.4).
    MatchDirector Director =>
        (ownerCafe != null ? ownerCafe : (ownerCafe = Cafe.Of(this)))?.Director;
    /// 이 칸이 지금 쓸 수 있는가. 「재료 선반」을 설치하기 전의 제조존 칸은 없는 것과 같다.
    public bool Enabled =>
        !needsShelfUpgrade || (Cafe.Of(this)?.HasUpgrade(UpgradeId.IngredientShelf) ?? false);

    /// 무엇이 얼마나 있는지는 그리드가 보여 준다 (기획서 6.5.4). 여기서는 F가 무엇을
    /// 하는지만 알린다.
    public string Prompt =>
        !Enabled ? string.Empty : GridOpen ? "재료 칸 · F로 닫기" : "재료 칸 · F로 열기";

    /// F는 그리드 창을 여닫는다. 꺼내기는 창에서 칸을 눌러 한다 (기획서 6.5.4: 낮의
    /// 재료 칸은 상자와 같은 그리드 UI에서 꺼낸다).
    ///
    /// 창을 여는 것은 `MatchFlow`다 — 이 값과 거리를 보고 띄우고 내린다. 여기서 직접
    /// 띄우지 않는 이유는 `BB.Game`이 UI를 알지 못하기 때문이다 (AGENTS.md 아키텍처).
    public void BeginInteractionClient()
    {
        if (!Enabled) return;
        GridOpen = !GridOpen;
    }

    /// 로컬 클라이언트가 이 칸의 그리드를 열어 뒀는가. 순환 인덱스와 같은 이유로 이 값도
    /// 클라이언트마다 다르다 — 창은 각자의 화면에만 있다.
    public bool GridOpen { get; private set; }

    /// 멀어졌거나 낮이 끝나서 창을 내렸다. 다시 다가왔을 때 저절로 열리지 않게 한다.
    public void CloseGridClient() => GridOpen = false;

    /// 창을 띄워 둘 거리. 서버의 `TakeRpc`가 쓰는 값과 같은 것을 봐야 손이 닿지 않는
    /// 칸을 누르는 창이 떠 있지 않는다.
    public float Reach => reach;

    /// 그리드에서 칸을 눌렀다 (기획서 6.5.4). 담을 수 있는지는 서버가 다시 판단한다 —
    /// 여기서 재고를 보고 거르면 그 검사는 클라이언트에 있는 것이라 근거가 되지 않는다.
    public void TakeSlotClient(int slot)
    {
        if (!Enabled || slot < 0 || slot >= offer.Length) return;

        index = slot;                   // 3D 강조가 가리키는 다음 칸도 여기서 이어진다
        TakeRpc((int)offer[slot]);
        ContentsChanged?.Invoke();      // 다음에 집힐 것이 옮겨 갔다
    }

    public void EndInteractionClient() { }

    /// 선반은 팀 재고를 그대로 늘어놓는다. 무엇을 캐 왔는지가 카페에서 눈에 보여야
    /// 밤과 낮이 이어진 것으로 읽힌다 (기획서 2장).
    public event System.Action ContentsChanged;
    public int SlotCount => Enabled ? offer.Length : 0;

    public CarryView SlotAt(int slot) =>
        Enabled && slot >= 0 && slot < offer.Length && Available(offer[slot])
            ? CarryView.Of(offer[slot])
            : CarryView.Nothing;

    // --- 그리드 창 (기획서 6.5.4) ---

    public Ingredient SlotItem(int slot) =>
        Enabled && slot >= 0 && slot < offer.Length ? offer[slot] : Ingredient.None;

    /// 상비 재료와 무한 공급 설비가 대는 재료는 세지 않는다 — `-1`이 무제한이다
    /// (`ILootGrid.SlotCountAt`, 기획서 7.1·8장).
    public int SlotCountAt(int slot)
    {
        if (!Enabled || slot < 0 || slot >= offer.Length) return 0;

        var item = offer[slot];
        if (Ingredients.IsStaple(item) || Unlimited(item)) return -1;
        return Stock != null ? Stock.CountOf(item) : 0;
    }

    /// 재료 칸은 아무것도 가리지 않는다. 순차 공개는 상자의 규칙이다 (기획서 6.5.5).
    public int RevealedCount => SlotCount;

    public string GridTitle => "재료 칸";

    public string GridHint => "칸을 눌러 손에 든다 · F로 닫기";

    /// 3D 선반에서 강조할 칸. 마지막으로 꺼낸 칸의 다음 재고다. **이 값만 클라이언트마다
    /// 다르다** — 인덱스가 로컬 상태라서다 (`IItemHolder.HighlightSlot`).
    public int HighlightSlot => Enabled ? NextAvailable(index) : -1;

    /// 손이 닿는지 재는 기준면 (`Station.WithinReach`).
    Collider surface;

    void Awake()
    {
        visuals = GetComponentsInChildren<Renderer>(true);
        surface = GetComponentInChildren<Collider>(true);
    }

    /// 로컬 플레이어가 이 칸에 손이 닿는가. 그리드 창을 띄워 둘지 판단하는 쪽이 쓴다
    /// (`MatchFlow.SyncShelfPopup`). 서버의 `TakeRpc`와 같은 자로 재야 손이 닿지 않는
    /// 칸을 누르는 창이 떠 있지 않는다.
    public bool LocalPlayerNear => Station.LocalPlayerNear(surface, transform, reach);

    public override void OnNetworkSpawn()
    {
        // 재고는 카페가 스폰될 때 이미 서 있다 (`Cafe.Awake`가 참조를 채운다).
        if (Stock != null) Stock.CountsChanged += OnStockChanged;

        var cafe = Cafe.Of(this);
        if (cafe != null) cafe.UpgradesChanged += OnUpgradesChanged;

        ApplyVisibility();
        ContentsChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        if (stock != null) stock.CountsChanged -= OnStockChanged;

        var cafe = Cafe.Of(this);
        if (cafe != null) cafe.UpgradesChanged -= OnUpgradesChanged;
    }

    void OnUpgradesChanged()
    {
        ApplyVisibility();
        ContentsChanged?.Invoke();
    }

    /// 설치 전 제조존 칸은 보이지 않는다. 보급존 본체는 항상 보인다.
    void ApplyVisibility()
    {
        if (!needsShelfUpgrade) return;

        var on = Enabled;
        for (var i = 0; i < visuals.Length; i++)
            if (visuals[i] != null) visuals[i].enabled = on;
    }

    void OnStockChanged() => ContentsChanged?.Invoke();


    /// 지연 해석한다. Cafe는 자기 참조를 Awake에서 채우는데 두 Awake 사이의 순서에
    /// 기대면 안 되기 때문이다.
    TeamStock Stock => stock != null ? stock : (stock = Cafe.Of(this)?.Stock);

    bool Available(Ingredient i) =>
        Ingredients.IsStaple(i) || Unlimited(i) || (Stock != null && Stock.CountOf(i) > 0);

    /// 「제빙기」와 「우유 탱크」는 그 재료를 무한 공급한다 (기획서 8장). 재고를 채우는
    /// 것이 아니라 재고를 묻지 않는 것이다 — 밤에 캐 온 얼음·우유는 그대로 남는다.
    bool Unlimited(Ingredient i)
    {
        var cafe = Cafe.Of(this);
        if (cafe == null) return false;

        return (i == Ingredient.Ice && cafe.HasUpgrade(UpgradeId.IceMaker))
            || (i == Ingredient.Milk && cafe.HasUpgrade(UpgradeId.MilkTank));
    }

    /// 팀 재고에 없는 것은 건너뛴다. F가 빈 이름표에 멈추지 않게 하기 위해서다.
    int NextAvailable(int from)
    {
        for (var n = 1; n <= offer.Length; n++)
        {
            var slot = ((from + n) % offer.Length + offer.Length) % offer.Length;
            if (Available(offer[slot])) return slot;
        }
        return -1;
    }

    [Rpc(SendTo.Server)]
    public void TakeRpc(int ingredient, RpcParams p = default)
    {
        var clientId = p.Receive.SenderClientId;
        if (!Enabled) return;                                // 아직 설치되지 않은 칸이다
        if (Director == null || Director.Phase.Current != Phase.Day) return;
        if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var c)) return;

        var po = c.PlayerObject;
        if (po == null) return;
        if (!Station.WithinReach(surface, transform, po.transform.position, reach)) return;
        if (!Cafe.SameTeamServer(this, clientId)) return;     // 내 재고가 아니다
        var carry = PlayerCarry.Of(clientId);
        if (carry == null) return;

        var want = (Ingredient)ingredient;
        if (System.Array.IndexOf(offer, want) < 0) return;   // 이 선반에 없는 재료다

        // 손은 기본으로 하나만 든다. 「양손잡이」는 *같은 재료*를 두 개까지 든다
        // (기획서 9.1). 서로 다른 재료를 한 손에 담지 않는 이유는 설비가 어느 쪽을
        // 받을지 F 하나로 고를 방법이 없기 때문이다.
        var pc = PlayerCharacter.Of(clientId);
        var limit = pc != null && pc.Has(DayPassive.Ambidextrous)
            ? DayPassives.AmbidextrousCarry : DayPassives.BaseCarry;

        if (!carry.CanTakeServer(want, limit)) return;

        // 상비 재료와 무한 공급 설비가 대는 재료는 재고를 묻지 않는다 (기획서 7.1, 8장).
        // 나머지는 밤에 캐 와야 한다.
        if (!Ingredients.IsStaple(want) && !Unlimited(want))
        {
            var larder = Stock;
            if (larder == null || !larder.TakeServer(want)) return;
        }

        carry.AddOneServer(want);
    }
}
