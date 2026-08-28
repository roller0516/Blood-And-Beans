using Unity.Netcode;
using UnityEngine;

/// 재료 칸 (기획서 5.1, 5.4). 낮 루프의 진입점이다.
///
/// 재고는 더 이상 무한하지 않다. 원두와 빵 베이스만 상비이고(기획서 7.1), 나머지는 밤에
/// 캐서 TeamStock에 들어온 것만 꺼낼 수 있다 — 이것이 밤과 낮을 잇는 고리다(기획서 2장).
public class IngredientShelf : NetworkBehaviour, IInteractable
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

    int index = -1;          // 첫 입력이 offer[0]에 오도록 -1에서 시작한다
    TeamStock stock;
    Cafe ownerCafe;

    /// 조립 루트는 전역이 아니라 소속 카페에서 받는다. 설비마다 따로 찾으면 카페별로
    /// 다른 답이 나올 여지가 생긴다 (아키텍처_v1.0.md §1.4).
    MatchDirector Director =>
        (ownerCafe != null ? ownerCafe : (ownerCafe = Cafe.Of(this)))?.Director;
    public string Prompt
    {
        get
        {
            var next = NextAvailable(index);
            return next < 0 ? "재료 칸 · 비어 있음" : $"재료 칸 · {offer[next]}{StockLabel(offer[next])}";
        }
    }

    public void BeginInteractionClient()
    {
        var next = NextAvailable(index);
        if (next < 0) return;
        index = next;
        TakeRpc((int)offer[index]);
    }

    public void EndInteractionClient() { }


    /// 지연 해석한다. Cafe는 자기 참조를 Awake에서 채우는데 두 Awake 사이의 순서에
    /// 기대면 안 되기 때문이다.
    TeamStock Stock => stock != null ? stock : (stock = Cafe.Of(this)?.Stock);

    bool Available(Ingredient i) =>
        Ingredients.IsStaple(i) || (Stock != null && Stock.CountOf(i) > 0);

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
        if (Director == null || Director.Phase.Current != Phase.Day) return;
        if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var c)) return;

        var po = c.PlayerObject;
        if (po == null) return;
        if (Vector3.Distance(po.transform.position, transform.position) > reach) return;
        if (!Cafe.SameTeamServer(this, clientId)) return;     // 내 재고가 아니다
        var carry = PlayerCarry.Of(clientId);
        if (carry == null || !carry.Empty) return;           // 한 번에 하나씩만 든다

        var want = (Ingredient)ingredient;
        if (System.Array.IndexOf(offer, want) < 0) return;   // 이 선반에 없는 재료다

        // 상비 재료는 항상 재고가 있다 (기획서 7.1). 나머지는 밤에 캐 와야 한다.
        if (!Ingredients.IsStaple(want))
        {
            var larder = Stock;
            if (larder == null || !larder.TakeServer(want)) return;
        }

        carry.GiveIngredientServer(want);
    }

    string StockLabel(Ingredient i) =>
        Ingredients.IsStaple(i) ? " (상비)" : $" x{(Stock != null ? Stock.CountOf(i) : 0)}";
}
