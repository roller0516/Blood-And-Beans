using Unity.Netcode;
using UnityEngine;

/// Dash bump (design doc 6.6). Not a skill — it is the only offensive verb of the night.
/// The owner only asks. The server picks the victim and decides the entire outcome.
public class DashHarass : NetworkBehaviour
{
    // ponytail: doc 14장 #7/#8 leave these undecided — mid-range placeholders.
    [SerializeField] float cooldown = 6f;        // doc: 5~8s
    [SerializeField] float reach = 1.6f;
    [SerializeField] float knockback = 3f;       // ~1.5 tiles
    [SerializeField] float spillShare = 0.1f;
    [SerializeField] float spillAtLoad = 0.8f;
    [SerializeField] float spawnProtectionSeconds = 15f;

    const float KnockSeconds = 0.15f;

    double nextDash;                             // server-side, never taken from the client

    float stunEnd;
    float knockStart;
    Vector3 knockFrom, knockTo;

    public bool IsStunnedServer => IsServer && Time.time < stunEnd;

    /// Runs after PlayerMove, so driving the position here overwrites this frame's input.
    /// That overwrite *is* the stun — knockback and lockout are the same motion.
    void LateUpdate()
    {
        if (!IsServer || Time.time >= stunEnd) return;

        transform.position = Vector3.Lerp(
            knockFrom, knockTo, Mathf.Clamp01((Time.time - knockStart) / KnockSeconds));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void DashRpc(RpcParams p = default)
    {
        var phase = MatchDirector.Find()?.Phase;
        if (phase == null || phase.Current != Phase.Night || phase.Day <= 1 ||
            phase.Elapsed < spawnProtectionSeconds) return;
        if (NetworkManager.ServerTime.Time < nextDash) return;
        nextDash = NetworkManager.ServerTime.Time + cooldown;

        var victim = NearestVictim();
        if (victim == null) return;

        var inv = victim.GetComponent<PlayerInventory>();
        var load = inv != null ? inv.LoadRatio : 0f;

        if (inv != null && load >= spillAtLoad) inv.DropShareServer(spillShare, victim.transform.position);
        victim.GetComponent<PlayerInteract>()?.InterruptServer();

        var dir = victim.transform.position - transform.position;
        dir.y = 0f;
        dir = dir.sqrMagnitude < 0.0001f ? transform.forward : dir.normalized;

        // A heavier victim staggers longer, capped at 1s (doc 6.6).
        victim.GetComponent<DashHarass>()
             ?.HitServer(dir * knockback, Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(load)));
    }

    void HitServer(Vector3 push, float stunSeconds)
    {
        if (!IsServer) return;
        knockFrom = transform.position;
        knockTo = transform.position + push;
        knockStart = Time.time;
        stunEnd = Time.time + stunSeconds;
    }

    NetworkObject NearestVictim()
    {
        NetworkObject best = null;
        var bestDist = reach;

        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            var player = client.PlayerObject;
            if (player == null || player == NetworkObject) continue;
            if (PlayerTeam.Of(player.OwnerClientId) == PlayerTeam.Of(OwnerClientId)) continue;

            var d = Vector3.Distance(transform.position, player.transform.position);
            if (d > bestDist) continue;
            best = player;
            bestDist = d;
        }
        return best;
    }
}
