using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// A forest loot box (doc 6.5). Scene-placed, so it spawns with the scene.
///
/// Contents are first-come, but opening and reveal state are private to each team.
///
/// **All timing is server-side.** The client says "I am holding F on this box" and nothing
/// else; opening and taking happen when the *server* clock says the hold was paid for.
/// It used to be the owner that measured the hold and then announced completion, which
/// made the entire night loot loop free (아키텍처_v1.0.md §1.1).
public class ItemBox : NetworkBehaviour, IInteractable
{
    [SerializeField] int tier = 1;              // 1..3
    [SerializeField] float openSeconds = 0.6f;
    [SerializeField] float takeSeconds = 0.2f;
    [SerializeField] float revealDelay = 1.5f;  // hidden slots uncover after this
    [SerializeField] float reach = 2.5f;
    [SerializeField] bool temporary;            // spilled pile, not a forest box

    readonly List<int> slots = new();             // server only
    bool[] openedByTeam = System.Array.Empty<bool>();
    double[] revealAtByTeam = System.Array.Empty<double>();

    int localTier;
    int[] localSlots = System.Array.Empty<int>();
    bool localOpened;
    double localRevealAt;

    readonly HoldTimer hold = new();
    readonly List<ulong> holders = new();

    MatchDirector director;

    public int Tier => localTier > 0 ? localTier : tier;
    public bool Opened => localOpened;
    public float Reach => reach;
    public string Prompt => Opened
        ? $"Tier {Tier} · {RemainingCount}/{SlotCount}" + (Revealed ? "" : " · 공개 중")
        : $"Tier {Tier} · 길게 눌러 열기";

    public void BeginInteractionClient()
    {
        var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        player?.GetComponent<PlayerInteract>()?.BeginBoxClient(this);
    }

    public void EndInteractionClient()
    {
        var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        player?.GetComponent<PlayerInteract>()?.EndBoxClient();
    }

    /// Boxes only exist for a player once the fog over them is gone (doc 6.1-2).
    /// Fog is per team, so "is this box visible" only means anything relative to a
    /// viewer. A pile you just dropped is in plain sight either way.
    public bool ClearedFor(int team)
    {
        if (temporary) return true;
        var f = director != null ? director.FogOf(team) : null;
        return f == null || f.IsRevealed(transform.position);
    }

    /// Slots past this index start hidden and uncover on a timer (doc 6.5.2).
    int VisibleCount => Tier switch { 1 => localSlots.Length, 2 => localSlots.Length - 1, _ => 2 };

    public bool Revealed =>
        localOpened && NetworkManager.ServerTime.Time >= localRevealAt;

    public override void OnNetworkSpawn()
    {
        director = MatchDirector.Find();
        if (director != null) director.Phase.PhaseEntered += OnPhaseEntered;
        if (!IsServer) return;
        openedByTeam = new bool[director != null ? director.TeamCount : 1];
        revealAtByTeam = new double[openedByTeam.Length];
        if (!temporary) ResetNightServer();
    }

    public override void OnNetworkDespawn()
    {
        if (director != null) director.Phase.PhaseEntered -= OnPhaseEntered;
        hold.CancelAll();
    }

    /// A spilled pile lasts the night and no longer (doc 6.7). Every box drops its holds
    /// at a phase boundary — a hold must not survive the night that started it.
    void OnPhaseEntered(Phase p)
    {
        hold.CancelAll();
        if (!IsServer) return;
        if (!temporary && p == Phase.Night) { ResetNightServer(); return; }
        if (temporary && p != Phase.Night && NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
    }

    void Update()
    {
        if (!IsServer) return;

        hold.CopyClientsTo(holders);
        for (var i = 0; i < holders.Count; i++) Tick(holders[i]);
    }

    /// One held key: pay the open time once, then one item per take time.
    void Tick(ulong clientId)
    {
        if (director == null || director.Phase.Current != Phase.Night)
        {
            hold.Cancel(clientId);
            return;
        }
        var team = PlayerTeam.Of(clientId);
        if (!InReach(clientId) || !ClearedFor(team)) { hold.Cancel(clientId); return; }

        var now = NetworkManager.ServerTime.Time;

        if (!OpenedFor(team))
        {
            // Rent tier 2+ slows opening (doc 3.3). Read with the *holder's* team.
            if (!hold.TryConsume(clientId, now, RequiredSecondsFor(team))) return;

            openedByTeam[team] = true;
            revealAtByTeam[team] = now + revealDelay;
            SendTeamStateServer(team);
            return;                              // the take clock starts from here
        }

        if (hold.TryConsume(clientId, now, takeSeconds)) TakeOne(clientId, team);
    }

    /// Takes the first remaining uncovered slot. Picking a specific slot is UI work
    /// (doc 6.5.1). ponytail: first-available until the box window exists.
    void TakeOne(ulong clientId, int team)
    {
        var inv = InventoryOf(clientId);
        if (inv == null) return;

        for (var i = 0; i < slots.Count; i++)
        {
            if (slots[i] == (int)Ingredient.None) continue;
            if (!IsSlotVisibleFor(i, team)) continue; // can't grab what hasn't uncovered yet

            inv.AddServer((Ingredient)slots[i]);
            slots[i] = (int)Ingredient.None;
            SendOpenedTeamsServer();
            return;
        }
    }

    /// Called by PlayerInteract on the server. Fog and distance are checked here so a
    /// client cannot register a hold on a box it is nowhere near.
    public void BeginHoldServer(ulong clientId)
    {
        if (!IsServer) return;
        if (director == null || director.Phase.Current != Phase.Night) return;
        if (!InReach(clientId) || !ClearedFor(PlayerTeam.Of(clientId))) return;
        hold.Begin(clientId, NetworkManager.ServerTime.Time);
    }

    public void CancelHoldServer(ulong clientId)
    {
        if (!IsServer) return;
        hold.Cancel(clientId);
    }

    /// A dash breaks off an open in progress but leaves half of it (doc 6.6).
    public void HalveHoldServer(ulong clientId)
    {
        if (!IsServer) return;
        hold.Halve(clientId, NetworkManager.ServerTime.Time);
    }

    /// Display only. The authoritative progress lives in `hold` on the server; showing
    /// the holder their own elapsed time does not need to be replicated.
    public float RequiredSecondsFor(int team)
    {
        if (IsServer ? OpenedFor(team) : localOpened) return takeSeconds;
        var ledger = director != null ? director.LedgerOf(team) : null;
        return openSeconds * (ledger != null ? ledger.BoxOpenScale : 1f);
    }

    /// Fills a pile with exactly what was spilled, already open — a dropped bag hides
    /// nothing (doc 6.7).
    public void SeedServer(IEnumerable<Ingredient> contents)
    {
        if (!IsServer) return;

        slots.Clear();
        foreach (var c in contents) slots.Add((int)c);
        for (var team = 0; team < openedByTeam.Length; team++) openedByTeam[team] = true;
        SendAllClientsStateServer(0d);
    }

    void Fill()
    {
        slots.Clear();
        var count = tier switch { 1 => 3, 2 => 4, _ => 5 };
        for (var i = 0; i < count; i++) slots.Add((int)RollForTier());
    }

    void ResetNightServer()
    {
        if (!IsServer || temporary) return;
        tier = Random.Range(1, 4);
        System.Array.Clear(openedByTeam, 0, openedByTeam.Length);
        System.Array.Clear(revealAtByTeam, 0, revealAtByTeam.Length);
        Fill();
        for (var team = 0; team < openedByTeam.Length; team++) SendTeamStateServer(team);
    }

    Ingredient RollForTier()
    {
        var common = new[]
        {
            Ingredient.Milk, Ingredient.Cream, Ingredient.Chocolate,
            Ingredient.Almond, Ingredient.Berry, Ingredient.Ice,
        };
        return common[Random.Range(0, common.Length)];
    }

    bool OpenedFor(int team) => team >= 0 && team < openedByTeam.Length && openedByTeam[team];

    bool IsSlotVisibleFor(int index, int team)
    {
        var visibleCount = tier switch { 1 => slots.Count, 2 => slots.Count - 1, _ => 2 };
        return index < visibleCount ||
            (OpenedFor(team) && NetworkManager.ServerTime.Time >= revealAtByTeam[team]);
    }

    /// Visible to a client either because the slot was never hidden, or the timer ran out.
    public bool IsSlotVisible(int index) => index < VisibleCount || Revealed;

    public Ingredient SlotContent(int index) =>
        index < 0 || index >= localSlots.Length ? Ingredient.None : (Ingredient)localSlots[index];

    public int SlotCount => localSlots.Length;

    public int RemainingCount
    {
        get
        {
            var n = 0;
            foreach (var s in localSlots) if (s != (int)Ingredient.None) n++;
            return n;
        }
    }

    public void SendStateToClientServer(ulong clientId, int team)
    {
        if (!IsServer || team < 0 || team >= openedByTeam.Length) return;
        var contents = openedByTeam[team] ? slots.ToArray() : System.Array.Empty<int>();
        BoxStateRpc(tier, openedByTeam[team], revealAtByTeam[team], contents,
            RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    void SendTeamStateServer(int team)
    {
        if (!IsServer || team < 0 || team >= openedByTeam.Length) return;
        foreach (var client in NetworkManager.ConnectedClientsList)
            if (PlayerTeam.Of(client.ClientId) == team)
                SendStateToClientServer(client.ClientId, team);
    }

    void SendOpenedTeamsServer()
    {
        for (var team = 0; team < openedByTeam.Length; team++)
            if (openedByTeam[team]) SendTeamStateServer(team);
    }

    void SendAllClientsStateServer(double revealAt)
    {
        foreach (var client in NetworkManager.ConnectedClientsList)
            BoxStateRpc(tier, true, revealAt, slots.ToArray(),
                RpcTarget.Single(client.ClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    void BoxStateRpc(int shownTier, bool isOpened, double shownRevealAt, int[] contents,
        RpcParams p = default)
    {
        localTier = shownTier;
        localOpened = isOpened;
        localRevealAt = shownRevealAt;
        localSlots = contents ?? System.Array.Empty<int>();
    }

    bool InReach(ulong clientId)
    {
        var t = Station.PlayerOf(clientId);
        return t != null && Vector3.Distance(t.position, transform.position) <= reach;
    }

    PlayerInventory InventoryOf(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.ConnectedClients.TryGetValue(clientId, out var c)) return null;
        return c.PlayerObject != null ? c.PlayerObject.GetComponent<PlayerInventory>() : null;
    }
}
