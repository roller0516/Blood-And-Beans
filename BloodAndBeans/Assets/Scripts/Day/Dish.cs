using Unity.Netcode;
using UnityEngine;

/// The whole dish supply as one component, not four objects (doc 5.3).
/// Counts are all the rules need: a dish is clean, in use, or dirty.
public class Dish : NetworkBehaviour
{
    // ponytail: 4 is the doc default; doc 14장 #5 leaves the real count undecided.
    [SerializeField] int total = 4;

    int baseTotal;

    readonly NetworkVariable<int> clean = new();
    readonly NetworkVariable<int> dirty = new();


    public int Clean => clean.Value;
    public int Dirty => dirty.Value;
    public int InUse => total - clean.Value - dirty.Value;

    public override void OnNetworkSpawn()
    {
        baseTotal = total;
        if (IsServer) clean.Value = total;
    }

    /// Called by Station when an order starts. False = no clean dish, no new order.
    public bool ClaimServer()
    {
        if (!IsServer || clean.Value <= 0) return false;
        clean.Value--;
        return true;
    }

    /// Served or thrown away — either way the dish comes back dirty.
    public void SoilServer()
    {
        if (!IsServer || InUse <= 0) return;   // only a dish that is actually out can come back
        dirty.Value++;
    }

    /// Rent tier 3 smashes one, for that day only (doc 3.3: every penalty lasts one
    /// day and lifts when rent is paid). Called every settlement with the current
    /// tier, so it restores as readily as it breaks.
    public void SetBreakageServer(bool broken)
    {
        if (!IsServer) return;

        var want = Mathf.Max(1, baseTotal - (broken ? 1 : 0));
        if (want == total) return;

        var delta = want - total;
        total = want;

        if (delta < 0)
        {
            if (clean.Value > 0) clean.Value--;
            else if (dirty.Value > 0) dirty.Value--;
        }
        else clean.Value++;
    }

    public void WashServer()
    {
        if (!IsServer || dirty.Value <= 0) return;
        dirty.Value--;
        clean.Value++;
    }

}
