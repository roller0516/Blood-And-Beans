using UnityEngine;

/// 마우스 회전 감도. 설정 팝업이 고른 배수를 카메라 축에 얹는다.
///
/// 기준값은 여기 적지 않는다. 축별 감도는 카메라 튜닝 값이고 출처는 `PlayerCameraRoot`의
/// `lookGain`이다 — 여기서는 배수만 곱한다. 기준을 코드에 복사하면 두 곳이 어긋난다.
///
/// 배수는 `PlayerPrefs`에 남는다. 설정 하나 때문에 저장 계층을 만들지 않는다.
///
/// 축과 같은 오브젝트에 산다. 플레이어가 스폰돼야 감도를 만질 대상이 생기므로,
/// `UISettingsPopup`은 이것을 못 찾으면 감도 줄을 감춘다 — 타이틀 화면에서 눌러도
/// 아무 일도 없는 슬라이더가 남지 않는다.
[RequireComponent(typeof(PlayerCameraRoot))]
public class LookSensitivity : MonoBehaviour
{
    const string PrefsKey = "look.sensitivity";

    /// 슬라이더 범위. 1이 `lookGain`에 저장된 그대로다.
    public const float Min = 0.25f;
    public const float Max = 3f;
    public const float Default = 1f;

    PlayerCameraRoot root;

    public float Multiplier { get; private set; } = Default;

    void Awake()
    {
        root = GetComponent<PlayerCameraRoot>();
        Apply(PlayerPrefs.GetFloat(PrefsKey, Default));
    }

    /// 배수를 적용하고 저장한다. 설정 팝업의 「적용」이 부른다.
    public void Apply(float multiplier)
    {
        Multiplier = Mathf.Clamp(multiplier, Min, Max);
        PlayerPrefs.SetFloat(PrefsKey, Multiplier);

        if (root != null) root.Sensitivity = Multiplier;
    }
}
