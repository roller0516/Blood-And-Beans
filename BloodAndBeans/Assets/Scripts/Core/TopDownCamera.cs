using Unity.Netcode;
using UnityEngine;

/// 로컬 플레이어를 따라가는 고정 각도 3D 탑다운 카메라.
public class TopDownCamera : MonoBehaviour
{
    [SerializeField] Vector3 offset = new(0f, 14f, -8f);
    [SerializeField] float pitch = 60f;

    [Header("흔들림")]
    /// 흔들림이 잦아드는 속도. 클수록 빨리 멎는다.
    [SerializeField] float shakeDamping = 14f;

    /// 이 아래로 내려가면 0으로 끊는다. 남겨 두면 카메라가 영원히 미세하게 떤다.
    const float ShakeEpsilon = 0.002f;

    Transform target;
    bool visionApplied;
    float shakeAmount;

    /// 화면을 짧게 흔든다. 세기는 미터 단위다.
    ///
    /// 겹치면 더하지 않고 더 센 쪽이 이긴다. 더하면 연타를 맞았을 때 화면이 튀어 나가
    /// 무슨 일이 일어났는지 도리어 안 보인다.
    public void Shake(float amount)
    {
        if (amount > shakeAmount) shakeAmount = amount;
    }

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

        // 흔들림은 추적이 끝난 뒤에 더한다. 위 대입식에 섞으면 다음 프레임의 기준 위치가
        // 흔들린 위치가 돼 카메라가 조금씩 표류한다.
        if (shakeAmount <= ShakeEpsilon)
        {
            shakeAmount = 0f;
            return;
        }

        transform.position += Random.insideUnitSphere * shakeAmount;
        shakeAmount = Mathf.Lerp(shakeAmount, 0f, 1f - Mathf.Exp(-shakeDamping * Time.deltaTime));
    }
}
