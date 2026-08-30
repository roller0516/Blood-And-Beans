using Unity.Cinemachine;
using UnityEngine;

/// 마우스 회전 감도. 설정 팝업이 고른 배수를 카메라 축 감도에 곱한다.
///
/// 기준값을 코드에 적지 않는다. 축마다의 감도는 카메라 튜닝 값이고 출처는 씬이다
/// (`MatchCameraBuilder.TppLookGain`이 넣고 씬에 저장된 값). 여기서는 그 값을 시작할 때
/// 한 번 읽어 두고 배수만 얹는다 — 기준을 코드에 복사하면 카메라를 다시 세울 때마다
/// 두 곳이 어긋난다.
///
/// 배수는 `PlayerPrefs`에 남는다. 설정 하나 때문에 저장 계층을 만들지 않는다.
[RequireComponent(typeof(CinemachineInputAxisController))]
public class LookSensitivity : MonoBehaviour
{
    const string PrefsKey = "look.sensitivity";

    /// 슬라이더 범위. 1이 씬에 저장된 그대로다.
    public const float Min = 0.25f;
    public const float Max = 3f;
    public const float Default = 1f;

    CinemachineInputAxisController controller;

    /// 씬에 저장돼 있던 축별 감도. 배수는 항상 이 값에 곱한다 — 지금 값에 곱하면
    /// 적용할 때마다 배수가 누적된다.
    float[] baseGains;

    public float Multiplier { get; private set; } = Default;

    /// `Awake`가 아니라 `Start`인 이유는 축 목록이 컨트롤러의 `OnEnable`에서 만들어지기
    /// 때문이다. 그전에 읽으면 목록이 비어 있어 기준값이 없는 채로 굳는다.
    void Start()
    {
        controller = GetComponent<CinemachineInputAxisController>();

        var axes = controller.Controllers;
        baseGains = new float[axes.Count];
        for (var i = 0; i < axes.Count; i++) baseGains[i] = axes[i].Input.Gain;

        Apply(PlayerPrefs.GetFloat(PrefsKey, Default));
    }

    /// 커서가 풀려 있는 동안에는 카메라를 돌리지 않는다. 슬롯을 클릭하려고 움직인
    /// 마우스가 시점까지 돌리면 창이 뜨는 순간 화면이 휙 돈다.
    ///
    /// 커서 잠금 상태를 신호로 쓰는 이유는 그것이 곧 "지금 마우스는 UI 것"이라는 뜻이기
    /// 때문이다 (`UIManager.ApplyInputGates`). 창마다 따로 이어 둘 것이 없다.
    ///
    /// 컴포넌트를 껐다 켜도 감도는 살아남는다. Cinemachine이 축 목록을 다시 만들 때
    /// 이름이 같은 컨트롤러를 재사용한다 (`InputAxisControllerBase.CreateControllers`의
    /// "recycling existing ones to preserve the settings").
    void Update()
    {
        if (controller == null) return;             // Start 전

        var look = Cursor.lockState == CursorLockMode.Locked;
        if (controller.enabled != look) controller.enabled = look;
    }

    /// 배수를 적용하고 저장한다. 설정 팝업의 「적용」이 부른다.
    public void Apply(float multiplier)
    {
        Multiplier = Mathf.Clamp(multiplier, Min, Max);
        PlayerPrefs.SetFloat(PrefsKey, Multiplier);

        if (baseGains == null) return;      // Start 전이면 저장만 하고, 시작할 때 반영된다

        var axes = controller.Controllers;
        for (var i = 0; i < axes.Count && i < baseGains.Length; i++)
            axes[i].Input.Gain = baseGains[i] * Multiplier;
    }
}
