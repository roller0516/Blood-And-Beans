using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// Hold F to wash dirty dishes (doc 5.3). Also where a burnt product gets binned:
/// throwing it away is 0 revenue and the dish goes straight to the dirty pile.
///
/// The wash time is measured on the server, like every other hold — the client used to
/// count it and then announce a finished wash (아키텍처_v1.0.md §1.1).
public class Sink : NetworkBehaviour, IInteractable
{
    // ponytail: placeholder, doc 14장 #5 leaves wash time undecided.
    [SerializeField] float washSeconds = 1.5f;
    [SerializeField] float reach = 2.5f;

    readonly HoldTimer hold = new();
    readonly List<ulong> holders = new();

    MatchDirector director;
    public string Prompt => "싱크대 · 길게 눌러 설거지 / 버리기로 폐기";

    public void BeginInteractionClient() => WashHoldBeginRpc();
    public void EndInteractionClient() => WashHoldEndRpc();

    public override void OnNetworkSpawn() => director = MatchDirector.Find();

    void Update()
    {
        if (!IsServer) return;

        hold.CopyClientsTo(holders);
        for (var i = 0; i < holders.Count; i++) Tick(holders[i]);
    }

    /// One dirty dish per wash time, for as long as F is held.
    void Tick(ulong clientId)
    {
        if (director == null || director.Phase.Current != Phase.Day)
        {
            hold.Cancel(clientId);
            return;
        }
        if (!InReach(clientId)) { hold.Cancel(clientId); return; }
        if (!hold.TryConsume(clientId, NetworkManager.ServerTime.Time, washSeconds)) return;
        Cafe.Of(this)?.Dishes?.WashServer();
    }

    [Rpc(SendTo.Server)]
    public void WashHoldBeginRpc(RpcParams p = default)
    {
        var clientId = p.Receive.SenderClientId;
        if (director == null || director.Phase.Current != Phase.Day) return;
        if (!InReach(clientId) || !Cafe.SameTeamServer(this, clientId)) return;
        hold.Begin(clientId, NetworkManager.ServerTime.Time);
    }

    [Rpc(SendTo.Server)]
    public void WashHoldEndRpc(RpcParams p = default) => hold.Cancel(p.Receive.SenderClientId);

    [Rpc(SendTo.Server)]
    public void DiscardRpc(RpcParams p = default)
    {
        var clientId = p.Receive.SenderClientId;
        if (director == null || director.Phase.Current != Phase.Day) return;
        if (!InReach(clientId) || !Cafe.SameTeamServer(this, clientId)) return;

        var carry = PlayerCarry.Of(clientId);
        if (carry == null || !carry.Held.IsProduct) return;

        carry.ClearServer();
        Cafe.Of(this)?.Dishes?.SoilServer();
    }

    public override void OnNetworkDespawn() => hold.CancelAll();

    bool InReach(ulong clientId)
    {
        var t = Station.PlayerOf(clientId);
        return t != null && Vector3.Distance(t.position, transform.position) <= reach;
    }

}
