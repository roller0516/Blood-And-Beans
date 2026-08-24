using Unity.Netcode;
using UnityEngine;

/// Fixed-angle 3D top-down camera that follows the local player.
public class TopDownCamera : MonoBehaviour
{
    [SerializeField] Vector3 offset = new(0f, 14f, -8f);
    [SerializeField] float pitch = 60f;
    [SerializeField] float smoothing = 8f;

    Transform target;
    bool visionApplied;

    void LateUpdate()
    {
        if (target == null)
        {
            var nm = NetworkManager.Singleton;
            var player = nm != null && nm.IsClient ? nm.LocalClient?.PlayerObject : null;
            if (player == null) return;
            target = player.transform;

            // Only your own cafe is yours to see (doc 3.1).
            if (!visionApplied)
            {
                var mine = player.GetComponent<PlayerTeam>();
                var director = MatchDirector.Find();
                TeamVision.ApplyServer(GetComponent<Camera>(), mine != null ? mine.Team : 0,
                                       director != null ? director.TeamCount : 1);
                visionApplied = true;
            }
        }

        transform.rotation = Quaternion.Euler(pitch, 0f, 0f);
        transform.position = Vector3.Lerp(
            transform.position, target.position + offset, smoothing * Time.deltaTime);
    }
}
