using Unity.Netcode;

/// What a player is carrying in the cafe.
public struct HeldItem
{
    public Ingredient Ingredient;    // raw ingredient in hand
    public bool IsProduct;
    public MenuId Menu;
    public Ingredient[] Recipe;
    public float GaugeMultiplier;
    public bool Burnt;

    public bool Empty => !IsProduct && Ingredient == Ingredient.None;

    /// default(HeldItem) would read as "holding Milk" — Ingredient.None is -1, not 0.
    public static HeldItem Nothing => new() { Ingredient = Ingredient.None };
}

/// One player's hands, server-side.
///
/// This was a `static Dictionary<ulong, HeldItem>` in Station.cs, which meant a
/// disconnected player's cup stayed in the map forever and the map outlived the play
/// session entirely (아키텍처_v1.0.md §1.5). On the player object it is destroyed with
/// the player, which is the whole fix.
///
/// ponytail: still server-only, so the held item is not visible to anyone else. That is
/// the same limitation as before — replicating it is presentation work and belongs with
/// the input/표현 split (아키텍처_v1.0.md §5, 5단계).
public class PlayerCarry : NetworkBehaviour
{
    HeldItem held = HeldItem.Nothing;

    public HeldItem Held => held;
    public bool Empty => held.Empty;

    public void SetServer(HeldItem item)
    {
        if (!IsServer) return;
        held = item;
    }

    public void ClearServer()
    {
        if (!IsServer) return;
        held = HeldItem.Nothing;
    }

    public void GiveIngredientServer(Ingredient i)
    {
        if (!IsServer) return;
        held = new HeldItem { Ingredient = i };
    }

    /// Null when the client is gone — which is exactly the case the static map could not
    /// represent. Callers must handle it rather than carrying on with a phantom hand.
    public static PlayerCarry Of(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.ConnectedClients.TryGetValue(clientId, out var c)) return null;
        return c.PlayerObject != null ? c.PlayerObject.GetComponent<PlayerCarry>() : null;
    }
}
