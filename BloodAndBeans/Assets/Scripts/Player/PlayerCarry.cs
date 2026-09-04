using Unity.Netcode;

/// 카페 안에서 플레이어가 들고 있는 것.
public struct HeldItem
{
    public Ingredient Ingredient;    // 손에 든 가공 전 재료
    public bool IsProduct;
    public MenuId Menu;
    public Ingredient[] Recipe;
    public float GaugeMultiplier;
    public bool Burnt;

    /// 손에 든 *재료*의 개수 (기획서 9.1 「양손잡이」: 한 번에 2개까지).
    /// 완성품은 늘 하나다 — 그릇이 하나뿐이라 두 잔이 한 손에 들릴 수 없다 (5.3).
    public int Count;

    /// 실제 개수. `default(HeldItem)`과 옛 코드가 만든 값이 0으로 오므로 최소 1로 읽는다.
    public int Amount => IsProduct ? 1 : (Count < 1 ? 1 : Count);

    public bool Empty => !IsProduct && Ingredient == Ingredient.None;

    /// default(HeldItem)은 "우유를 들고 있음"으로 읽힌다. Ingredient.None은 0이 아니라 -1이다.
    public static HeldItem Nothing => new() { Ingredient = Ingredient.None };

    /// 재료 하나짜리 손.
    public static HeldItem Of(Ingredient i, int count = 1) =>
        new() { Ingredient = i, Count = count < 1 ? 1 : count };
}

/// 플레이어 한 명의 손. 서버 측이다.
///
/// 원래는 Station.cs 안의 `static Dictionary<ulong, HeldItem>`이었다. 그래서 접속이 끊긴
/// 플레이어의 컵이 맵에 영원히 남았고, 그 맵 자체가 플레이 세션보다 오래 살아남았다
/// (아키텍처_v1.0.md §1.5). 플레이어 오브젝트에 올리면 플레이어와 함께 파괴된다.
/// 그게 수정의 전부다.
///
/// 규칙 판정에 쓰는 `HeldItem`은 서버만 든다 — `Recipe`가 관리 배열이라 복제할 수 없다.
/// 대신 이름표에 필요한 만큼만 `CarryView`로 복제한다. 낮의 조작은 "재료를 옮기는 것"이
/// 전부라(기획서 5.1) 무엇을 들었는지가 안 보이면 2인 분업 자체가 성립하지 않는다.
public class PlayerCarry : NetworkBehaviour, IItemHolder
{
    HeldItem held = HeldItem.Nothing;

    /// 표시용. 서버가 쓰고 전원이 읽는다. 초기값을 명시하는 이유는 `default`가
    /// "우유를 들고 있음"으로 읽히기 때문이다 (`CarryView.Nothing` 주석).
    readonly NetworkVariable<CarryView> view = new(CarryView.Nothing);

    public HeldItem Held => held;
    public bool Empty => held.Empty;

    /// 팀원 화면이 읽는 값. 서버의 `Held`와 항상 같은 시점에 바뀐다.
    public CarryView View => view.Value;

    /// 손은 한 번에 하나만 든다 (`IngredientShelf.TakeRpc`의 `!carry.Empty` 검사).
    public event System.Action ContentsChanged;
    public int SlotCount => 1;
    public CarryView SlotAt(int index) => index == 0 ? view.Value : CarryView.Nothing;
    public int HighlightSlot => -1;

    MatchDirector director;
    GamePhase subscribedPhase;

    /// 표현이 구독을 걸 자리. 서버에도 걸린다 — 호스트의 화면도 클라이언트 화면이다.
    public override void OnNetworkSpawn()
    {
        view.OnValueChanged += OnViewChanged;
        MatchDirector.Bind(BindDirector);
        ContentsChanged?.Invoke();      // 스폰 시점의 값으로 한 번 그린다
    }

    public override void OnNetworkDespawn()
    {
        view.OnValueChanged -= OnViewChanged;

        MatchDirector.Unbind(BindDirector);
        if (subscribedPhase != null) subscribedPhase.PhaseEntered -= OnPhaseEntered;
        subscribedPhase = null;
    }

    /// 같은 인스턴스로 두 번 불려도 되게 짠다 (`MatchDirector.Bind` 계약).
    void BindDirector(MatchDirector next)
    {
        director = next;
        var phase = next != null ? next.Phase : null;
        if (phase == subscribedPhase) return;

        if (subscribedPhase != null) subscribedPhase.PhaseEntered -= OnPhaseEntered;
        subscribedPhase = phase;
        if (subscribedPhase != null) subscribedPhase.PhaseEntered += OnPhaseEntered;
    }

    /// 낮이 끝나면 손을 비운다 (기획서 4장: 카페 영업은 낮에 끝난다).
    ///
    /// 가방은 밤마다 비우는데(`PlayerInventory.OnPhaseEntered`) **손을 비우는 곳은 한
    /// 군데도 없었다.** 그래서 낮에 집은 재료가 전환·밤을 넘어 다음 낮까지 그대로 남았다.
    ///
    /// **완성품이었으면 그릇을 함께 돌려보낸다.** 그릇은 조리를 시작할 때 나가서
    /// (`Station.Insert` -> `Dish.ClaimServer`) 서빙하거나 버릴 때만 돌아오는데
    /// (`Counter`, `Sink`), 손에 든 채로 낮이 끝나면 돌아올 길이 없어 영영 InUse로 남는다.
    /// 그릇이 다 나가면 그 팀은 **어느 기계에도 재료를 넣을 수 없다** (기획서 5.3).
    void OnPhaseEntered(Phase p)
    {
        if (!IsServer || p == Phase.Day || held.Empty) return;

        if (held.IsProduct)
            director?.CafeOf(PlayerTeam.Of(OwnerClientId))?.Dishes?.SoilServer();

        ClearServer();
    }

    void OnViewChanged(CarryView _, CarryView __) => ContentsChanged?.Invoke();

    public void SetServer(HeldItem item)
    {
        if (!IsServer) return;
        held = item;
        view.Value = CarryView.Of(item);
    }

    /// 손에 든 재료 하나를 덜어낸다. 마지막 하나였으면 손이 빈다.
    ///
    /// 「양손잡이」가 2개를 들고 있어도 설비에는 하나씩 들어간다 (기획서 9.1이 늘린 것은
    /// 운반 개수이지 조합 규칙이 아니다). 두 개를 한꺼번에 넣으면 같은 재료가 두 칸이 되어
    /// 메뉴 표와 매칭되지 않고(`Menus.Match`) 완성품이 「정체불명」이 된다.
    public void ConsumeOneServer()
    {
        if (!IsServer || held.Empty || held.IsProduct) { ClearServer(); return; }

        var left = held.Amount - 1;
        if (left <= 0) { ClearServer(); return; }

        held.Count = left;
        view.Value = CarryView.Of(held);
    }

    /// 이 재료를 하나 더 받을 수 있는가. 빈손이거나 같은 재료를 한도 미만으로 들고 있을 때다.
    public bool CanTakeServer(Ingredient want, int limit) =>
        held.Empty || (!held.IsProduct && held.Ingredient == want && held.Amount < limit);

    /// 같은 재료를 하나 더 든다.
    public void AddOneServer(Ingredient want)
    {
        if (!IsServer) return;

        held = held.Empty
            ? HeldItem.Of(want)
            : new HeldItem { Ingredient = want, Count = held.Amount + 1 };

        view.Value = CarryView.Of(held);
    }

    public void ClearServer()
    {
        if (!IsServer) return;
        held = HeldItem.Nothing;
        view.Value = CarryView.Nothing;
    }

    /// 클라이언트가 사라졌으면 null이다. static 맵이 표현하지 못하던 바로 그 경우다.
    /// 호출자는 유령 손을 들고 계속 진행하지 말고 이 경우를 처리해야 한다.
    public static PlayerCarry Of(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.ConnectedClients.TryGetValue(clientId, out var c)) return null;
        return c.PlayerObject != null ? c.PlayerObject.GetComponent<PlayerCarry>() : null;
    }
}
