using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 캐릭터 선택 화면의 팀 칸 하나. 트리는 `UITeamSlot.prefab`에 있고
/// <see cref="UICharacterSelectScreen"/>이 팀 수만큼 이어 둔다.
///
/// 칸은 자기가 몇 번째 팀인지 모른다. 이름표와 색은 화면이 넘기고, 누름도 화면이 준
/// 콜백으로 돌려준다.
public sealed class UITeamSlot : MonoBehaviour
{
    /// 고르지 않은 칸이 팀 색을 패널 바닥 쪽으로 얼마나 죽이는가.
    /// 색을 완전히 지우지 않는 이유는 어느 칸이 무슨 팀인지 계속 보여야 하기 때문이다.
    const float DimAmount = 0.65f;

    [SerializeField] Button button;
    [SerializeField] Image background;
    [SerializeField] TMP_Text label;

    Color teamColor = Color.white;

    /// 칸을 이을 때 한 번. 이름과 색은 프리팹에 박힌 값이 아니라 화면에서 온다.
    public void Bind(string teamName, Color color, Action click)
    {
        if (label != null) label.text = teamName;
        teamColor = color;
        UIButtons.Wire(button, click);
        SetSelected(false);
    }

    public void SetSelected(bool on)
    {
        if (background != null)
            background.color = on ? teamColor : Color.Lerp(teamColor, UITheme.Panel, DimAmount);
    }
}
