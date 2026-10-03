using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 들고 있는 식기 위에 뜨는 제조 카드 (기획서 5.7.3). 윗줄은 「지금 무엇인가」, 아랫줄은
/// 「무엇이 들어갔는가」다. 재료 오브젝트(5.3.1)가 갖춰지기 전까지의 임시 표시다.
///
/// 복제된 손(`CarryView.Parts`)만 읽는다. 판정은 서버가 이미 했고 여기는 같은 표를 보고 그린다.
public sealed class UIMakingCard : MonoBehaviour
{
    enum State { Ready, Making, Spoiled }

    [SerializeField] Image frame;
    [SerializeField] Image glow;

    /// 메뉴 아이콘을 받치는 원판. 아이콘이 없으면 같이 숨긴다.
    [SerializeField] GameObject menuBadge;
    [SerializeField] Image menuIcon;
    [SerializeField] TMP_Text check;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text statusText;
    [SerializeField] RectTransform partsRow;
    [SerializeField] Image partTemplate;
    [SerializeField] GameObject plusTemplate;

    /// 쓰레기 테두리. 테마 팔레트에 탁한 갈색이 없어서 여기 둔다.
    [SerializeField] Color spoiledColor = new(0.42f, 0.32f, 0.21f);

    [SerializeField, Range(0f, 1f)] float glowAlpha = 0.4f;

    readonly List<Ingredient> parts = new();
    readonly List<Image> partIcons = new();
    readonly List<GameObject> pluses = new();
    CarryView last;
    bool drawn;
    bool hasContent;

    /// 담긴 것이 없으면 false다. 같은 손이면 다시 그리지 않는다.
    public bool Render(in CarryView view)
    {
        if (drawn && view.Equals(last)) return hasContent;
        drawn = true;
        last = view;

        var count = view.PartCount;
        hasContent = count > 0;
        if (!hasContent) return false;

        parts.Clear();
        for (var i = 0; i < count; i++) parts.Add(view.PartAt(i));

        var menu = Menus.Match(parts);
        var state = menu != MenuId.None ? State.Ready : Menus.CanComplete(parts) ? State.Making : State.Spoiled;
        var theme = UITheme.Config;
        var blood = parts.Contains(Ingredient.BloodBean);

        frame.color = state switch { State.Ready => theme.GoldLit, State.Making => theme.Cream, _ => spoiledColor };

        // 블러드 빈은 남에게도 새는 정보라 카드도 붉게 샌다 (1.4, 3.4).
        glow.gameObject.SetActive(state == State.Ready || blood);
        var glowColor = blood ? theme.Red : theme.GoldLit;
        glowColor.a = glowAlpha;
        glow.color = glowColor;
        check.gameObject.SetActive(state == State.Ready);

        var icon = state == State.Ready ? ResourceManager.Instance.MenuSprite(menu) : null;
        menuIcon.sprite = icon;
        menuBadge.SetActive(icon != null);

        nameText.text = state switch { State.Ready => DisplayNames.Of(menu), State.Making => "만드는 중", _ => "쓰레기" };
        statusText.text = StatusOf(state, view);

        EnsureSlots(count);
        for (var i = 0; i < partIcons.Count; i++)
        {
            var used = i < count;
            partIcons[i].gameObject.SetActive(used);
            pluses[i].SetActive(used && i > 0);
            if (used) partIcons[i].sprite = ResourceManager.Instance.IngredientSprite(parts[i]);
        }
        return true;
    }

    // ponytail: 상태 문구는 기획서 5.7.3에 「한 줄짜리 상태 문구」로만 있다. 확정되면 여기만 고친다.
    static string StatusOf(State state, in CarryView view) => state switch
    {
        State.Ready when view.Burnt => "탄 것",
        State.Ready => view.IsProduct ? "서빙 가능" : "조리하면 완성",
        State.Making => "재료를 더 넣는다",
        _ => "B로 쏟는다",
    };

    /// 빈 칸은 그리지 않는다 — 깔아 둔 칸 수가 약속으로 읽힌다 (5.7.3). 들어간 수만큼만 늘린다.
    void EnsureSlots(int count)
    {
        while (partIcons.Count < count)
        {
            var plus = Instantiate(plusTemplate, partsRow);
            var icon = Instantiate(partTemplate, partsRow);
            pluses.Add(plus);
            partIcons.Add(icon);
        }
    }
}
