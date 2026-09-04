using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 첫 화면. 로비로 들어가거나 게임을 끝낸다.
///
/// 예전에는 이 화면과 방 목록·대기실이 한 클래스 안의 패널 세 개였고, 어느 것을 보여
/// 줄지는 `Page` enum과 `SetActive` 세 줄이 정했다. 화면이 하나 늘 때마다 그 세 줄과
/// enum을 같이 고쳐야 했다. 이제 화면 하나가 프리팹 하나다.
public sealed class UITitleMenuScreen : UIScreen
{
    [SerializeField] TMP_Text heading;
    [SerializeField] Button lobbyButton;
    [SerializeField] Button settingsButton;
    [SerializeField] Button quitButton;

    /// 화면은 자기가 무엇을 할지 모른다. 눌렸다는 사실만 Presenter에 넘긴다.
    public void Bind(string title, Action openRooms, Action openSettings, Action quit)
    {
        if (heading != null) heading.text = title;
        UIButtons.Wire(lobbyButton, openRooms);
        UIButtons.Wire(settingsButton, openSettings);
        UIButtons.Wire(quitButton, quit);
    }
}
