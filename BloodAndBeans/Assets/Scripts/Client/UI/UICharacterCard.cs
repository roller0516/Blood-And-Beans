using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 캐릭터 선택 화면의 카드 한 장 (기획서 9장). 트리는 `UICharacterCard.prefab`에 있고
/// <see cref="UICharacterSelectScreen"/>이 `CharacterCatalog.All` 수만큼 찍어 낸다.
///
/// 카드를 프리팹으로 뺀 이유는 캐릭터 종 수가 14장 #10 미결이기 때문이다. 화면 프리팹에
/// 8장을 깔아 두면 종 수가 바뀔 때마다 트리를 손으로 고쳐야 한다.
///
/// 글자 배율은 이 카드가 스스로 먹인다 — 화면 루트의 <see cref="UIFontScale"/>는 자기
/// Awake에서 자식을 한 번만 모으므로, 그 뒤에 태어나는 카드는 거기 잡히지 않는다.
[RequireComponent(typeof(UIFontScale))]
public sealed class UICharacterCard : MonoBehaviour
{
    /// 선점 칩의 좌우 여백. 칩은 이름 길이에 맞춰 줄인다 — 고정 폭이면 짧은 이름에서 빈 칸이 남는다.
    const float ChipPadding = 38f;

    [SerializeField] Button button;
    [SerializeField] Image background;
    [SerializeField] TMP_Text label;
    [SerializeField] GameObject claimRoot;
    [SerializeField] RectTransform claimChip;
    [SerializeField] Image claimDot;
    [SerializeField] TMP_Text claimLabel;

    /// 카드를 만들 때 한 번. 이름은 프리팹에 박힌 값이 아니라 카탈로그에서 온다.
    public void Bind(string characterName, Action click)
    {
        if (label != null) label.text = characterName;
        UIButtons.Wire(button, click);
    }

    /// 고른 카드는 지금 고른 팀 색으로 칠한다 — 목업 2번의 네임플레이트 팔레트와 같은 색이라
    /// "내가 고른 것"과 "내 팀 색"이 한 화면에서 이어진다.
    public void SetSelected(bool on, Color teamColor)
    {
        if (background != null) background.color = on ? teamColor : UITheme.Panel;
    }

    /// 남이 집어 간 표시 (기획서 9.1 중복 픽 금지). <paramref name="owner"/>가 null이면 빈 칸이다.
    public void SetClaim(string owner, Color color)
    {
        var taken = owner != null;
        if (button != null) button.interactable = !taken;
        if (claimRoot != null) claimRoot.SetActive(taken);
        if (!taken) return;

        if (claimDot != null) claimDot.color = color;
        if (claimLabel == null) return;

        claimLabel.text = owner;
        if (claimChip != null)
            claimChip.sizeDelta = new Vector2(
                claimLabel.preferredWidth + ChipPadding, claimChip.sizeDelta.y);
    }
}
