using Unity.Netcode;
using UnityEngine;

/// Hold F at a box: the first hold opens it, continued holding takes one item at a time
/// (doc 6.5.1). Opening is short; the time sink is taking.
///
/// This class no longer decides when a hold is finished. It reports "I started holding
/// this box" and "I let go", and the server times the rest — the owner measuring its own
/// hold made the night loot loop free (아키텍처_v1.0.md §1.1). It is also the one place
/// that knows which box a player is working on, which is what the dash interrupt needs.
public class PlayerInteract : NetworkBehaviour
{
    ItemBox held;           // owner-side: what we have told the server about
    ItemBox serverHeld;     // server-side truth, for the dash interrupt

    public void BeginBoxClient(ItemBox box)
    {
        if (!IsOwner || box == null || held == box) return;
        EndBoxClient();
        held = box;
        if (held != null) HoldBeginRpc(held.NetworkObject);
    }

    public void EndBoxClient()
    {
        if (!IsOwner || held == null) return;
        HoldEndRpc();
        held = null;
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer) ReleaseServer();
    }

    /// Sender is checked against the owner: an `[Rpc(SendTo.Server)]` on a NetworkBehaviour
    /// can be invoked by any client, and a missing check of exactly this kind is what let
    /// one team stop another team's completion gauge (아키텍처_v1.0.md §1.2).
    [Rpc(SendTo.Server)]
    void HoldBeginRpc(NetworkObjectReference box, RpcParams p = default)
    {
        if (p.Receive.SenderClientId != OwnerClientId) return;
        if (!box.TryGet(out var no)) return;

        var target = no.GetComponent<ItemBox>();
        if (target == null) return;

        ReleaseServer();
        serverHeld = target;
        target.BeginHoldServer(OwnerClientId);
    }

    [Rpc(SendTo.Server)]
    void HoldEndRpc(RpcParams p = default)
    {
        if (p.Receive.SenderClientId != OwnerClientId) return;
        ReleaseServer();
    }

    void ReleaseServer()
    {
        if (serverHeld != null) serverHeld.CancelHoldServer(OwnerClientId);
        serverHeld = null;
    }

    /// A dash breaks off an open in progress but leaves half the progress (doc 6.6).
    /// The progress lives on the box now, so the interrupt goes there.
    public void InterruptServer()
    {
        if (!IsServer || serverHeld == null) return;
        serverHeld.HalveHoldServer(OwnerClientId);
    }

}
