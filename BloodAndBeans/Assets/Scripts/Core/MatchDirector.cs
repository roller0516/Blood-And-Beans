using UnityEngine;
using Unity.Netcode;

/// The one authority on "how many teams are in this match, and who is on which".
///
/// Six components used to answer that independently — two scene-serialized `teamId`
/// fields, a modulo of the client id, a count of `Cafe` objects, a count of tills, and a
/// read of that count (아키텍처_v1.0.md §1.4). Every team-isolation defect this project
/// has had came out of those six disagreeing. Now the slots are declared here, in order,
/// and every other component is *told* its team.
///
/// Deliberately **not** a NetworkBehaviour: the roster is scene data, identical on every
/// peer, so team ids are derived locally rather than replicated. Only the ledgers and the
/// seating counter are server-side, and neither is sent anywhere.
///
/// It owns the roster and the per-team ledger. It does not own the clock — `GamePhase`
/// does, on this same object, and is exposed here only so a component that already had
/// to find the director does not have to search twice.
[RequireComponent(typeof(GamePhase))]
public class MatchDirector : MonoBehaviour
{
    /// Team id **is** the index into these. Order in the inspector is the order of teams;
    /// the first empty slot ends the roster.
    [SerializeField] Cafe[] cafeSlots = new Cafe[0];
    [SerializeField] FogOfWar[] fogSlots = new FogOfWar[0];

    TeamLedger[] ledgers = new TeamLedger[0];
    GamePhase phase;
    int teamCount;
    int nextSeat;

    public GamePhase Phase => phase;
    public int TeamCount => teamCount;

    /// The one lookup. Scene-unique, resolved once by each consumer at spawn — never per
    /// frame, and never cached in a static (아키텍처_v1.0.md §1.5/1.6).
    public static MatchDirector Find() => FindFirstObjectByType<MatchDirector>();

    void Awake()
    {
        phase = GetComponent<GamePhase>();
        teamCount = Mathf.Max(1, CountConfiguredSlots());

        // Fresh every time the scene loads. The dictionary this replaced was static and
        // carried one match's debts into the next.
        ledgers = new TeamLedger[teamCount];
        for (var i = 0; i < teamCount; i++) ledgers[i] = new TeamLedger();
        nextSeat = 0;

        // Push the team down instead of letting each object guess it. This runs on every
        // peer, not just the server: the slot array is scene data and identical
        // everywhere, so a client derives the same answer without a round trip.
        for (var team = 0; team < teamCount; team++)
        {
            if (team < cafeSlots.Length && cafeSlots[team] != null) cafeSlots[team].AssignTeam(team);
            if (team < fogSlots.Length && fogSlots[team] != null) fogSlots[team].AssignTeam(team);
        }
    }

    /// Slots are contiguous from 0: the first empty one ends the roster. A gap would make
    /// "team 2" mean two different things depending on who was counting.
    int CountConfiguredSlots()
    {
        var n = 0;
        while (n < cafeSlots.Length && cafeSlots[n] != null) n++;
        return n;
    }

    public Cafe CafeOf(int team) =>
        team >= 0 && team < cafeSlots.Length ? cafeSlots[team] : null;

    public FogOfWar FogOf(int team) =>
        team >= 0 && team < fogSlots.Length ? fogSlots[team] : null;

    /// Server-side. Never null for a team the roster knows about.
    public TeamLedger LedgerOf(int team) =>
        team >= 0 && team < ledgers.Length ? ledgers[team] : null;

    /// Seat the next player. ponytail: round-robin by join order until the lobby exists
    /// (기획서 10장 puts team size there). Round-robin rather than pairing so that a
    /// two-player session still has two teams to test isolation with.
    public int SeatServer() => nextSeat++ % Mathf.Max(1, teamCount);

    public void ApplyTeamVisibilityServer(ulong clientId, int team)
    {
        var manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer) return;

        for (var cafeTeam = 0; cafeTeam < teamCount; cafeTeam++)
        {
            var cafe = CafeOf(cafeTeam);
            if (cafe == null) continue;
            foreach (var networkObject in cafe.GetComponentsInChildren<NetworkObject>(true))
            {
                if (!networkObject.IsSpawned || !networkObject.IsNetworkVisibleTo(clientId)) continue;
                if (cafeTeam != team) networkObject.NetworkHide(clientId);
            }
        }

        foreach (var customer in FindObjectsByType<Customer>(FindObjectsSortMode.None))
            if (customer.TeamId == team && customer.NetworkObject.IsSpawned &&
                !customer.NetworkObject.IsNetworkVisibleTo(clientId))
                customer.NetworkObject.NetworkShow(clientId);
    }

    public void ShowToTeamServer(NetworkObject networkObject, int team)
    {
        var manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer || networkObject == null || !networkObject.IsSpawned) return;
        foreach (var client in manager.ConnectedClientsList)
            if (PlayerTeam.Of(client.ClientId) == team && !networkObject.IsNetworkVisibleTo(client.ClientId))
                networkObject.NetworkShow(client.ClientId);
    }

    void OnValidate()
    {
        // A null between two filled slots silently shortens the roster, and the teams
        // past the gap then have no cafe at all. Fail loudly in the editor instead.
        for (var i = 1; i < cafeSlots.Length; i++)
            if (cafeSlots[i] != null && cafeSlots[i - 1] == null)
                Debug.LogError($"{name}: cafeSlots has a gap at {i - 1}; slots must be contiguous.", this);
    }
}
