using Unity.Netcode;
using UnityEngine;

/// A team's return point (doc 6.8). At the moment the night ends, anyone not standing in
/// their own team's zone loses half of what they carried — and whatever survives becomes
/// the team's larder for tomorrow (doc 2장). Before that deposit existed the night's haul
/// was simply discarded and the core loop was open.
public class ReturnZone : NetworkBehaviour
{
    [SerializeField] float radius = 4f;

    Cafe cafe;
    MatchDirector director;

    /// The cafe, not its team id. MatchDirector assigns team ids in its own Awake and the
    /// order between two Awakes is not something to depend on; the parent walk is stable
    /// either way. Duplicating the cafe used to copy a serialized 0 into both zones, so
    /// team 1's zone judged team 0's players and stripped half their haul while they
    /// stood in their own doorway.
    void Awake() => cafe = Cafe.Of(this);

    int TeamId => cafe != null ? cafe.TeamId : -1;

    /// Settling on the phase event rather than polling `phase.Current` is what makes the
    /// order against TransitionLedger defined: PhaseEntered fires inside GamePhase.Enter,
    /// so every deposit has landed before any Update observes the new phase and draws the
    /// forecast from team stock (doc 5.5 rule 3).
    public override void OnNetworkSpawn()
    {
        director = MatchDirector.Find();
        if (IsServer && director != null) director.Phase.PhaseEntered += OnPhaseEntered;
    }

    public override void OnNetworkDespawn()
    {
        if (director != null) director.Phase.PhaseEntered -= OnPhaseEntered;
    }

    void OnPhaseEntered(Phase p)
    {
        if (!IsServer || p != Phase.Transition) return;
        Settle();
    }

    void Settle()
    {
        var team = TeamId;
        if (team < 0) return;      // unassigned zone judges nobody

        var stock = cafe != null ? cafe.Stock : null;

        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            var player = client.PlayerObject;
            if (player == null || TeamOf(player) != team) continue;

            var inv = player.GetComponent<PlayerInventory>();
            if (inv == null) continue;

            // Missing the zone costs half, lost outright — nobody can pick it up (6.8).
            if (Vector3.Distance(player.transform.position, transform.position) > radius)
                inv.LoseHalfServer();

            var haul = inv.DrainServer();
            if (stock == null) continue;
            for (var i = 0; i < haul.Count; i++)
            {
                // 상비 재료는 재고로 세지 않는다 (doc 7.1) — 선반이 무한으로 준다.
                if (Ingredients.IsStaple(haul[i])) continue;
                stock.DepositServer(haul[i]);
            }
        }
    }

    static int TeamOf(NetworkObject player)
    {
        var t = player != null ? player.GetComponent<PlayerTeam>() : null;
        return t != null ? t.Team : -1;
    }
}
