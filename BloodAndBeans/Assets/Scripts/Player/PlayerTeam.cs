using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// Which cafe this player belongs to. Teams are 1-2 players, so with two cafes the
/// first two clients are team 0 and 1 respectively; a third and fourth pair up.
public class PlayerTeam : NetworkBehaviour
{
    readonly NetworkVariable<int> team = new();

    public int Team => team.Value;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        // The roster is not this component's business any more — counting cafes here was
        // one of the six places that answered "how many teams" (아키텍처_v1.0.md §1.4).
        var director = MatchDirector.Find();
        if (director == null)
        {
            Debug.LogError($"{name}: no MatchDirector in the scene; the player cannot be "
                         + "seated and every team check will fail.", this);
            return;
        }

        team.Value = director.SeatServer();
        director.ApplyTeamVisibilityServer(OwnerClientId, team.Value);
        foreach (var box in FindObjectsByType<ItemBox>(FindObjectsSortMode.None))
            box.SendStateToClientServer(OwnerClientId, team.Value);
        director.FogOf(team.Value)?.SendSnapshotToClientServer(OwnerClientId);
        StartCoroutine(ApplyVisibilityAfterSceneSpawn(director));
    }

    IEnumerator ApplyVisibilityAfterSceneSpawn(MatchDirector director)
    {
        yield return null;
        director.ApplyTeamVisibilityServer(OwnerClientId, team.Value);
    }

    /// Which team a given client is on. Server-side answer — the lookups that used to
    /// live in ItemBox asked the *local* client instead, which meant the server priced
    /// a box open with the wrong team's penalty (아키텍처_v1.0.md §1.1).
    /// Failure is not a team. Returning a valid team id here turned a missing component
    /// into team 0 fixture access.
    public static int Of(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.ConnectedClients.TryGetValue(clientId, out var c)) return -1;

        var t = c.PlayerObject != null ? c.PlayerObject.GetComponent<PlayerTeam>() : null;
        return t != null ? t.Team : -1;
    }

    /// The team this client belongs to. Display and local input gating only.
    public static int Local()
    {
        var nm = NetworkManager.Singleton;
        var po = nm != null && nm.IsClient && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;

        var t = po != null ? po.GetComponent<PlayerTeam>() : null;
        return t != null ? t.Team : -1;
    }
}
