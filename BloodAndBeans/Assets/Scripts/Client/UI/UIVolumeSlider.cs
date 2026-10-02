using UnityEngine;
using UnityEngine.UI;

/// 설정 팝업의 볼륨 한 줄. 어느 버스를 맡는지는 프리팹 인스턴스가 정한다.
public sealed class UIVolumeSlider : MonoBehaviour
{
    [SerializeField] SoundBus bus;
    [SerializeField] Slider slider;

    /// 저장된 값을 보여 준다. 적용 전까지는 소리를 바꾸지 않는다.
    public void Show() => slider.SetValueWithoutNotify(SoundManager.Instance.GetVolume(bus));

    public void Apply() => SoundManager.Instance.SetVolume(bus, slider.value);
}
