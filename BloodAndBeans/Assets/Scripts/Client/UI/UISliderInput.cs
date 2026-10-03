using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 슬라이더 옆 숫자 칸. 둘이 같은 값을 쥐고 서로를 따라간다.
public sealed class UISliderInput : MonoBehaviour
{
    [SerializeField] Slider slider;
    [SerializeField] TMP_InputField input;

    /// 칸에 보이는 값 = 슬라이더 값 × 이 배수. 음량은 0~1을 0~100으로 보여 준다.
    [SerializeField, Min(0.0001f)] float displayScale = 1f;
    [SerializeField] string format = "0.00";

    void Awake()
    {
        slider.onValueChanged.AddListener(Show);
        input.onEndEdit.AddListener(OnTyped);
    }

    void OnDestroy()
    {
        slider.onValueChanged.RemoveListener(Show);
        input.onEndEdit.RemoveListener(OnTyped);
    }

    /// `SetValueWithoutNotify`로 맞춘 뒤 부른다. 알림이 없으면 칸이 따라오지 않는다.
    public void Refresh() => Show(slider.value);

    void Show(float value) =>
        input.SetTextWithoutNotify((value * displayScale).ToString(format, CultureInfo.InvariantCulture));

    /// 슬라이더에 넘기면 범위 밖 값은 슬라이더가 잘라 주고, 그 결과가 다시 칸에 찍힌다.
    void OnTyped(string text)
    {
        if (float.TryParse(text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            slider.value = value / displayScale;
        Refresh();
    }
}
