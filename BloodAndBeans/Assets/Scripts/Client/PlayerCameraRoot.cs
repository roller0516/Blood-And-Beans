using UnityEngine;

/// 카메라가 따라가는 회전 축. Unity Starter Assets의 `ThirdPersonController`가 들고 있는
/// `CinemachineCameraTarget`과 같은 자리이며, 회전 계산도 그 `CameraRotation()`을 옮긴 것이다.
///
/// **플레이어의 자식이라 위치는 부모가 준다. 여기서 정하는 것은 회전뿐이다.**
/// `CinemachineThirdPersonFollow`가 Follow 대상의 회전을 그대로 쓰는데, 캐릭터 몸은
/// 이동 방향으로 돌아가므로(`PlayerMove.StepMove`) 몸과 분리된 축이 있어야 마우스가
/// 개입할 자리가 생긴다. 이 축 없이 몸을 직접 따르게 하면 원을 그리며 걷는 것만으로
/// 시야가 통째로 360° 돈다 (실측: 몸 359° → 카메라 359°).
///
/// **`ThirdPersonController`를 통째로 쓰지 않는 이유는 그 클래스가 이동까지 소유하기
/// 때문이다.** 중력·점프·접지를 클라이언트에서 굴리며 매 프레임 `CharacterController.Move`를
/// 부른다. 이 프로젝트의 이동은 서버 권위 + 소유자 예측이고 중력이 없다
/// (`PlayerMove`, `PlayerPrediction`). 둘을 같이 붙이면 권위가 깨진다.
public class PlayerCameraRoot : MonoBehaviour
{
    [Header("Cinemachine")]
    /// 위아래로 볼 수 있는 한계. Starter Assets의 TopClamp/BottomClamp와 같다.
    [SerializeField] float topClamp = 55f;
    [SerializeField] float bottomClamp = -15f;

    /// 최종 pitch에 더하는 보정. 리그를 안 건드리고 화면을 조금 내려다보게 할 때 쓴다.
    [SerializeField] float cameraAngleOverride;

    /// 켜면 마우스를 무시한다. 컷신처럼 시점을 묶어야 할 때 쓴다.
    [SerializeField] bool lockCameraPosition;

    /// 마우스 델타에 곱하는 값. 세로가 음수인 것이 반전 없음이다 — pitch는 값이 커질수록
    /// 아래를 보므로, 마우스를 올려 위를 보려면 줄어야 한다.
    ///
    /// **마우스 델타에 바로 곱한다. `Time.deltaTime`을 쓰지 않는다** (Starter Assets의
    /// `CameraRotation` 주석 "Don't multiply mouse input by Time.deltaTime"과 같은 이유 —
    /// 델타는 이미 프레임당 이동량이라 또 곱하면 프레임레이트에 따라 감도가 달라진다).
    /// 예전 값 (3, -1.5)는 `CinemachineInputAxisController`가 자체 가감속과 시간 보정을
    /// 하던 시절 것이라, 그대로 두면 지금 구조에서는 세 배로 빠르다.
    [SerializeField] Vector2 lookGain = new(1f, -0.5f);

    /// 이보다 작은 입력은 버린다 (Starter Assets의 `_threshold`).
    const float Threshold = 0.01f;

    float yaw;
    float pitch;

    void Start()
    {
        var angles = transform.rotation.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
    }

    /// 이번 프레임의 마우스 델타. `PlayerInputRouter`가 소유자일 때만 넣는다
    /// (Starter Assets의 `StarterAssetsInputs.look` 자리).
    public Vector2 Look { private get; set; }

    /// 감도 배수. `LookSensitivity`가 설정 팝업의 값을 넣는다.
    public float Sensitivity { private get; set; } = 1f;

    /// `CinemachineBrain`이 평범한 `LateUpdate`로 돌기 때문에 순서가 미정이다. Starter
    /// Assets도 `LateUpdate`에서 축을 돌리고, 카메라가 한 프레임 늦지 않게 앞세운다.
    void LateUpdate()
    {
        if (Look.sqrMagnitude >= Threshold && !lockCameraPosition)
        {
            yaw += Look.x * lookGain.x * Sensitivity;
            pitch += Look.y * lookGain.y * Sensitivity;
        }

        yaw = ClampAngle(yaw, float.MinValue, float.MaxValue);
        pitch = ClampAngle(pitch, bottomClamp, topClamp);

        transform.rotation = Quaternion.Euler(pitch + cameraAngleOverride, yaw, 0f);
    }

    /// Starter Assets의 `ClampAngle` 그대로.
    static float ClampAngle(float angle, float min, float max)
    {
        if (angle < -360f) angle += 360f;
        if (angle > 360f) angle -= 360f;
        return Mathf.Clamp(angle, min, max);
    }
}
