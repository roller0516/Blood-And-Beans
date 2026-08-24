using Unity.Netcode;
using UnityEngine;

/// The serving counter (doc 5.4). Only reachable from the manufacturing side —
/// whoever is below the prep island cannot serve.
public class Counter : NetworkBehaviour, IInteractable
{
    [SerializeField] float reach = 2.5f;
    MatchDirector director;
    public string Prompt => "서빙대 · 서빙하기";

    public void BeginInteractionClient() => ServeRpc();
    public void EndInteractionClient() { }

    public override void OnNetworkSpawn() => director = MatchDirector.Find();

    /// Behind the counter, i.e. the side its -forward points at.
    bool OnServingSide(Vector3 p)
    {
        var d = p - transform.position;
        return d.magnitude <= reach && Vector3.Dot(d, transform.forward) < 0f;
    }

    [Rpc(SendTo.Server)]
    public void ServeRpc(RpcParams p = default)
    {
        var clientId = p.Receive.SenderClientId;
        if (director == null || director.Phase.Current != Phase.Day) return;
        var t = Station.PlayerOf(clientId);
        if (t == null || !OnServingSide(t.position)) return;
        if (!Cafe.SameTeamServer(this, clientId)) return;     // not your counter

        var carry = PlayerCarry.Of(clientId);
        if (carry == null || !carry.Held.IsProduct) return;

        var queue = Cafe.Of(this)?.Queue;
        if (queue != null && queue.TryServeServer(carry.Held))
            carry.ClearServer();          // no taker? keep carrying it
    }

}
