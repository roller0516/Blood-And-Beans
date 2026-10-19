using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// 안내 문구 하나와 확인 버튼뿐인 팝업. 문구는 부르는 쪽이 정한다.
/// 뒤의 화면 입력은 프리팹의 InputBlocker가 막는다.
public sealed class UIMessagePopup : UIPopup
{
    [SerializeField] TMP_Text message;
    [SerializeField] Button okButton;

    GameObject previousSelection;

    protected override void Awake()
    {
        base.Awake();
        if (message == null || okButton == null)
            CDebug.LogError($"{name}: {nameof(message)} 또는 {nameof(okButton)}이 비어 있다.", this);
    }

    public void Bind(string text, Action close)
    {
        message.text = text;
        previousSelection = EventSystem.current?.currentSelectedGameObject;
        UIButtons.Wire(okButton, () =>
        {
            close?.Invoke();
            EventSystem.current?.SetSelectedGameObject(previousSelection);
        });
        EventSystem.current?.SetSelectedGameObject(okButton.gameObject);
    }
}
