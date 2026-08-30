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

    public bool Empty => !IsProduct && Ingredient == Ingredient.None;

    /// default(HeldItem)은 "우유를 들고 있음"으로 읽힌다. Ingredient.None은 0이 아니라 -1이다.
    public static HeldItem Nothing => new() { Ingredient = Ingredient.None };
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
public class PlayerCarry : NetworkBehaviour
{
    HeldItem held = HeldItem.Nothing;

    /// 표시용. 서버가 쓰고 전원이 읽는다. 초기값을 명시하는 이유는 `default`가
    /// "우유를 들고 있음"으로 읽히기 때문이다 (`CarryView.Nothing` 주석).
    readonly NetworkVariable<CarryView> view = new(CarryView.Nothing);

    public HeldItem Held => held;
    public bool Empty => held.Empty;

    /// 팀원 화면이 읽는 값. 서버의 `Held`와 항상 같은 시점에 바뀐다.
    public CarryView View => view.Value;

    public void SetServer(HeldItem item)
    {
        if (!IsServer) return;
        held = item;
        view.Value = CarryView.Of(item);
    }

    public void ClearServer()
    {
        if (!IsServer) return;
        held = HeldItem.Nothing;
        view.Value = CarryView.Nothing;
    }

    public void GiveIngredientServer(Ingredient i)
    {
        if (!IsServer) return;
        held = new HeldItem { Ingredient = i };
        view.Value = CarryView.Of(held);
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
