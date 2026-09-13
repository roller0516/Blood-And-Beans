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
public sealed class UICharacterCard : MonoBehaviour
{
    /// 선점 칩의 좌우 여백. 칩은 이름 길이에 맞춰 줄인다 — 고정 폭이면 짧은 이름에서 빈 칸이 남는다.
    const float ChipPadding = 38f;

    [SerializeField] Button button;
    [SerializeField] Image background;
    [SerializeField] TMP_Text label;

    /// 종류 아이콘. FBX가 들어오기 전에는 스프라이트가 없어 자리색만 보인다.
    [SerializeField] Image portrait;
    [SerializeField] GameObject selectedMark;
    [SerializeField] GameObject claimRoot;
    [SerializeField] RectTransform claimChip;
    [SerializeField] TMP_Text claimLabel;

    /// 카드를 만들 때 한 번. 이름은 카탈로그에서, 아이콘은 `CharacterVisualConfig`에서 온다.
    /// <paramref name="icon"/>이 null이면 아직 아트가 안 들어온 칸이라 자리색만 남는다.
    public void Bind(string characterName, Sprite icon, Action click)
    {
        if (label != null) label.text = characterName;

        if (portrait != null)
        {
            portrait.sprite = icon;
            portrait.preserveAspect = true;
            portrait.color = icon != null ? Color.white : UITheme.Placeholder;
        }

        UIButtons.Wire(button, click);
    }

    /// 이 칸을 누가 쥐고 있는가. 테두리 색이 곧 그 사람의 팀 색이고, 방의 모두가 같은
    /// 색을 본다 — 어느 팀이 무엇을 골랐는지 한눈에 갈리게 하려는 것이다.
    bool selected;
    Color selectedColor;
    bool claimed;
    Color claimColor;

    /// 내가 고른 칸. 내 팀 색으로 테두리를 칠한다.
    public void SetSelected(bool on, Color teamColor)
    {
        selected = on;
        if (selectedMark != null) selectedMark.SetActive(on);
        selectedColor = teamColor;
        RefreshBorder();
    }

    /// 남이 집어 간 표시. <paramref name="owner"/>가 null이면 빈 칸이다.
    ///
    /// 보여 주는 것과 잠그는 것이 갈린다 — 방의 누가 골랐든 이름은 뜨지만,
    /// 못 고르게 막는 것은 같은 팀의 픽뿐이다 (기획서 9.1 중복 픽 금지).
    ///
    /// 잠근 칸을 회색으로 죽이지 않는다. Button의 ColorTint 전이를 끈 것이 이 때문이다 —
    /// 켜 두면 `interactable = false`가 테두리를 DisabledColor로 덮어써 팀 색이 사라진다.
    public void SetClaim(string owner, Color teamColor, bool locked)
    {
        claimed = owner != null;
        claimColor = teamColor;

        if (button != null) button.interactable = !locked;
        if (claimRoot != null) claimRoot.SetActive(claimed);
        if (claimed && claimLabel != null) claimLabel.text = owner;

        RefreshBorder();
    }

    /// 남의 픽이 내 선택보다 우선한다. 같은 칸을 둘이 보고 있으면 이미 쥔 쪽이 임자다.
    void RefreshBorder()
    {
        if (background == null) return;
        background.color = claimed ? claimColor : selected ? selectedColor : UITheme.Panel;
    }
}
