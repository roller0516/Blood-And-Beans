using Unity.Netcode;
using UnityEngine;

/// Each player sees only their own cafe (doc 3.1: a rival's ingredients, equipment and
/// characters are private — only their revenue is public).
///
/// ponytail: this culls rendering, it does not stop replication. The client still
/// receives the other cafe's state. Swap to NetworkObject.NetworkHide per client if
/// the hiding ever has to be authoritative.
public class TeamVision : MonoBehaviour
{
    public const string LayerPrefix = "CafeTeam";

    /// Applied by the local player's camera once it knows which team it belongs to.
    public static void ApplyServer(Camera cam, int myTeam, int teamCount)
    {
        if (cam == null) return;

        var mask = cam.cullingMask;
        for (var t = 0; t < teamCount; t++)
        {
            var layer = LayerMask.NameToLayer(LayerPrefix + t);
            if (layer < 0) continue;                 // layer not defined — nothing to cull
            if (t == myTeam) mask |= 1 << layer;
            else mask &= ~(1 << layer);
        }
        cam.cullingMask = mask;
    }
}
