using Unity.Netcode;
using UnityEngine;

/// 로컬 플레이어를 따라가는 고정 각도 3D 탑다운 카메라.
public class TopDownCamera : MonoBehaviour
{
    [SerializeField] Vector3 offset = new(0f, 14f, -8f);
    [SerializeField] float pitch = 60f;

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

            // 볼 수 있는 것은 자기 카페뿐이다 (기획서 3.1).
            if (!visionApplied)
            {
                var mine = player.GetComponent<PlayerTeam>();
                var director = MatchDirector.Instance;
                TeamVision.ApplyServer(GetComponent<Camera>(), mine != null ? mine.Team : 0,
                                       director != null ? director.TeamCount : 1);
                visionApplied = true;
            }
        }

        transform.rotation = Quaternion.Euler(pitch, 0f, 0f);

        // 고정각 탑다운이라 카메라가 플레이어에 그대로 붙는다. 지수 추적(Lerp)을 쓰면
        // 등속 이동 중 speed/smoothing 만큼 영구히 뒤처져(5/8 = 0.625m) 화면 전체가
        // 밀린 채로 끌려다녔다. 탑다운에서 카메라 지연은 곧 조작 지연으로 읽힌다.
        transform.position = target.position + offset;
    }
}
