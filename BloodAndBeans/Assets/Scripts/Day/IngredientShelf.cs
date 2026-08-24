using Unity.Netcode;
using UnityEngine;

/// The 재료 칸 (doc 5.1, 5.4). Entry point of the day loop.
///
/// Stock is no longer infinite. 원두와 빵 베이스만 상비이고(doc 7.1), 나머지는 밤에 캐서
/// TeamStock에 들어온 것만 꺼낼 수 있다 — 이것이 밤과 낮을 잇는 고리다(doc 2장).
public class IngredientShelf : NetworkBehaviour, IInteractable
{
    /// What this shelf is willing to hand out, in cycle order. Availability is a
    /// separate question, answered by the larder.
    [SerializeField] Ingredient[] offer =
    {
        Ingredient.Bean, Ingredient.BreadBase, Ingredient.Milk,
        Ingredient.Cream, Ingredient.Chocolate, Ingredient.Almond,
        Ingredient.Berry, Ingredient.Ice,
    };
    [SerializeField] float reach = 2.5f;

    int index = -1;          // -1 so the first press lands on offer[0]
    TeamStock stock;
    MatchDirector director;
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

    public override void OnNetworkSpawn() => director = MatchDirector.Find();

    /// Resolved lazily: Cafe fills its own references in Awake and the order between
    /// two Awakes is not something to depend on.
    TeamStock Stock => stock != null ? stock : (stock = Cafe.Of(this)?.Stock);

    bool Available(Ingredient i) =>
        Ingredients.IsStaple(i) || (Stock != null && Stock.CountOf(i) > 0);

    /// Skips what the larder does not have, so F never lands on an empty label.
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
        if (director == null || director.Phase.Current != Phase.Day) return;
        if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var c)) return;

        var po = c.PlayerObject;
        if (po == null) return;
        if (Vector3.Distance(po.transform.position, transform.position) > reach) return;
        if (!Cafe.SameTeamServer(this, clientId)) return;     // not your larder
        var carry = PlayerCarry.Of(clientId);
        if (carry == null || !carry.Empty) return;           // one thing at a time

        var want = (Ingredient)ingredient;
        if (System.Array.IndexOf(offer, want) < 0) return;   // not on this shelf

        // Staples are always stocked (doc 7.1); everything else had to be farmed.
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
