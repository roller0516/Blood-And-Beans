using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// 설정 팝업. 현재 값을 보여 주고 적용할 때만 실제 설정을 바꾼다.
///
/// 타이틀에서도 매치 중(ESC)에서도 같은 팝업이 뜬다. 마우스 감도 줄은 감도를 가진
/// 카메라가 씬에 있을 때만 보인다 — 타이틀에는 돌릴 카메라가 없다.
public sealed class UISettingsPopup : UIPopup
{
    [SerializeField] Slider masterVolume;
    [SerializeField] Toggle fullscreen;
    [SerializeField] Button applyButton;
    [SerializeField] Button closeButton;

    [Header("마우스 감도")]
    [SerializeField] Slider lookSensitivity;
    [SerializeField] TMP_Text lookLabel;

    GameObject previousSelection;

    /// 설정을 여는 동안은 조작을 통째로 접는다. 슬라이더를 잡고 있는 사이에 캐릭터가
    /// 걸어가거나 대시가 나가면, 밤중에 창 하나 여는 것이 그대로 위험이 된다.
    public override bool BlocksPlayerInput => true;

    /// 열 때 한 번 찾는다. 매치 씬에만 있고 팝업은 씬을 넘어 살아남으므로 직렬화로
    /// 이을 수 없다. 주기 실행이 아닌 한 번짜리 탐색이다 (AGENTS.md 참조와 결합도).
    LookSensitivity look;

    public void Bind(Action close)
    {
        previousSelection = EventSystem.current?.currentSelectedGameObject;
        if (masterVolume != null) masterVolume.SetValueWithoutNotify(AudioListener.volume);
        if (fullscreen != null) fullscreen.SetIsOnWithoutNotify(Screen.fullScreen);
        BindLook();

        UIButtons.Wire(closeButton, () => Close(close));
        UIButtons.Wire(applyButton, () => Apply(close));
        if (applyButton != null) EventSystem.current?.SetSelectedGameObject(applyButton.gameObject);
    }

    /// 감도 줄을 씬 상태에 맞춘다. 돌릴 카메라가 없으면 줄 자체를 감춘다 — 눌러도
    /// 아무 일도 없는 슬라이더가 남아 있으면 고장으로 읽힌다.
    void BindLook()
    {
        look = FindAnyObjectByType<LookSensitivity>();

        var available = look != null;
        if (lookLabel != null) lookLabel.gameObject.SetActive(available);
        if (lookSensitivity == null) return;

        lookSensitivity.gameObject.SetActive(available);
        if (!available) return;

        lookSensitivity.minValue = LookSensitivity.Min;
        lookSensitivity.maxValue = LookSensitivity.Max;
        lookSensitivity.SetValueWithoutNotify(look.Multiplier);
    }

    void Apply(Action close)
    {
        if (masterVolume != null) AudioListener.volume = Mathf.Clamp01(masterVolume.value);
        if (fullscreen != null) Screen.fullScreen = fullscreen.isOn;
        if (look != null && lookSensitivity != null) look.Apply(lookSensitivity.value);
        Close(close);
    }

    void Close(Action close)
    {
        close?.Invoke();
        EventSystem.current?.SetSelectedGameObject(previousSelection);
    }
}
