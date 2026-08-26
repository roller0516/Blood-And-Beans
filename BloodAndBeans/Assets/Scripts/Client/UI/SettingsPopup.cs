using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// 타이틀 화면의 최소 설정 팝업. 현재 값을 보여 주고 적용할 때만 시스템 설정을 바꾼다.
public sealed class SettingsPopup : UIPopup
{
    [SerializeField] Slider masterVolume;
    [SerializeField] Toggle fullscreen;
    [SerializeField] Button applyButton;
    [SerializeField] Button closeButton;

    GameObject previousSelection;

    public void Bind(Action close)
    {
        previousSelection = EventSystem.current?.currentSelectedGameObject;
        if (masterVolume != null) masterVolume.SetValueWithoutNotify(AudioListener.volume);
        if (fullscreen != null) fullscreen.SetIsOnWithoutNotify(Screen.fullScreen);

        UIButtons.Wire(closeButton, () => Close(close));
        UIButtons.Wire(applyButton, () => Apply(close));
        if (applyButton != null) EventSystem.current?.SetSelectedGameObject(applyButton.gameObject);
    }

    void Apply(Action close)
    {
        if (masterVolume != null) AudioListener.volume = Mathf.Clamp01(masterVolume.value);
        if (fullscreen != null) Screen.fullScreen = fullscreen.isOn;
        Close(close);
    }

    void Close(Action close)
    {
        close?.Invoke();
        EventSystem.current?.SetSelectedGameObject(previousSelection);
    }
}
