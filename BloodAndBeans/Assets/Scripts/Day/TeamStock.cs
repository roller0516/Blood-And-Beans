using Unity.Netcode;
using UnityEngine;

/// One team's larder — what the night hauled back is what the day can sell (doc 2장).
///
/// This is the link that closed the core loop: before it existed, IngredientShelf handed
/// out every ingredient forever and nothing a team dug up at night mattered.
///
/// 원두와 빵 베이스는 들어오지 않는다. 상비 재료라 선반이 무한으로 준다 (doc 7.1).
public class TeamStock : NetworkBehaviour
{
    /// Indexed by (int)Ingredient. Staple slots stay 0 and are never read.
    readonly NetworkList<int> counts = new();

    static readonly int Slots = System.Enum.GetValues(typeof(Ingredient)).Length;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        counts.Clear();                       // a respawn must not stack a second table
        for (var i = 0; i < Slots; i++) counts.Add(0);
    }

    public int CountOf(Ingredient i)
    {
        var slot = (int)i;
        return slot < 0 || slot >= counts.Count ? 0 : counts[slot];
    }

    public void DepositServer(Ingredient i)
    {
        if (!IsServer) return;

        var slot = (int)i;
        if (slot < 0 || slot >= counts.Count) return;
        counts[slot] += 1;
    }

    /// False when the larder is empty — that menu simply cannot be made today.
    public bool TakeServer(Ingredient i)
    {
        if (!IsServer) return false;

        var slot = (int)i;
        if (slot < 0 || slot >= counts.Count || counts[slot] <= 0) return false;
        counts[slot] -= 1;
        return true;
    }

    /// Everything the team currently holds, for the 30% slice of the order forecast
    /// (doc 5.5 rule 3). Allocates — called once per transition, not per frame.
    public void CopyHeldTo(System.Collections.Generic.List<Ingredient> outp)
    {
        for (var slot = 0; slot < counts.Count; slot++)
        {
            if (counts[slot] <= 0) continue;
            var i = (Ingredient)slot;
            if (!outp.Contains(i)) outp.Add(i);
        }
    }
}
