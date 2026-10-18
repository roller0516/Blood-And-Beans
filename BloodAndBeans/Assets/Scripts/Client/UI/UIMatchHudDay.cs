using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 매치 HUD의 낮 쪽 — 상단 띠(순위·시계·매출)·조합식 패널·보석·제조 카드·완성 게이지.
/// `DayRoot`에 붙고 낮에만 켜진다. 밤과 같이 쓰는 칸은 `UIMatchHudScreen`이 갖는다.
public sealed class UIMatchHudDay : MonoBehaviour
{
    [Header("상단 띠")]
    [SerializeField] TMP_Text dayClock;
    [SerializeField] TMP_Text dayRevenue;
    [SerializeField] UIStandingRow[] dayStandingRows = System.Array.Empty<UIStandingRow>();
    [SerializeField] TMP_Text dayCounter;
    [SerializeField] TMP_Text dayGoal;
    [SerializeField] Image dayTimeGauge;
    [SerializeField] GameObject dayRentBadge;
    [SerializeField] Color accent = new(0.91f, 0.77f, 0.42f);       // 금색 강조
    [SerializeField] Color alarmColor = new(0.93f, 0.35f, 0.28f);
    [SerializeField] Color rentMetColor = new(0.45f, 0.85f, 0.5f);
    // ponytail: 경고 시점은 PDF 목업의 30초. 밸런스 확정 시 인스펙터에서 조정한다.
    [SerializeField] float rentWarningSeconds = 30f;

    [Header("조합식 — F1로 여닫는 중앙 패널")]
    /// 화면 중앙 패널. HUD 요소에 가리지 않게 `RecipeWrap`이 `DayRoot`의 맨 위 자식이다.
    [SerializeField] GameObject recipePanel;
    [SerializeField] Button recipeTabButton;

    /// 탭에 적는 단축키 안내.
    [SerializeField] TMP_Text recipeTabGlyph;
    [SerializeField] UIRecipeRow recipeRowPrefab;
    [SerializeField] RectTransform recipeContent;
    [SerializeField] RectTransform recipeDessertContent;

    /// 보석 칸. 순서는 `Gems.All`과 같다. 상한 6이 기획서로 고정이라 프리팹에 미리 깔아 둔다 (5.7.1).
    [SerializeField] UIGemIcon[] gemIcons = System.Array.Empty<UIGemIcon>();

    [Header("제조 카드")]
    /// 제조 카드 (기획서 5.7.3). 나와 팀원 둘이라 처음 쓸 때 두 장을 만든다.
    [SerializeField] UIMakingCard makingCardPrefab;

    /// 내 카드의 고정 자리. 좌하단 줄이라 레이아웃이 스킬 슬롯 옆에 놓는다.
    [SerializeField] RectTransform makingCardDock;

    /// 팀원 카드가 식기 자리에서 뜨는 높이(m).
    [SerializeField, Min(0f)] float makingCardHeight = 0.45f;

    [Header("완성 게이지")]
    /// 기획서 5.2의 침 게이지. 구간 폭은 `CompletionGauge`가 판정에 쓰는 값을 그대로 받아
    /// 그린다 — 여기 따로 적으면 보이는 구간과 맞는 구간이 어긋난다.
    [SerializeField] RectTransform completionBar;
    [SerializeField] RectTransform completionGoodZone;
    [SerializeField] RectTransform completionPerfectZone;
    [SerializeField] RectTransform completionNeedle;
    [SerializeField] TMP_Text completionLabel;

    UIRecipeRow[] recipeRows;
    UIMakingCard[] makingCards;
    bool recipeAvailable;

    public bool RecipeOpen => recipePanel != null && recipePanel.activeSelf;

    void Awake()
    {
        // 메뉴표는 판 중에 바뀌지 않는다. 화면이 재사용되므로 한 번만 만든다.
        recipeRows = new UIRecipeRow[Menus.All.Length];
        for (var i = 0; i < Menus.All.Length; i++)
        {
            var menu = Menus.All[i];
            var dessert = System.Array.IndexOf(menu.Parts, Menus.DessertBase) >= 0;
            var parent = dessert && recipeDessertContent != null ? recipeDessertContent : recipeContent;
            recipeRows[i] = Instantiate(recipeRowPrefab, parent);
            recipeRows[i].Bind(menu);
        }
        if (gemIcons.Length != Gems.All.Length)
            CDebug.LogError($"{name}: 보석 칸이 {gemIcons.Length}개다. {Gems.All.Length}개를 이어야 한다.", this);
        UIButtons.Wire(recipeTabButton, () => ToggleRecipe());
        SetRecipeOpen(false);
    }

    public void Render(in MatchHudModel model)
    {
        recipeAvailable = true;

        for (var i = 0; i < dayStandingRows.Length; i++)
        {
            var visible = model.Standings != null && i < model.Standings.Count;
            var row = dayStandingRows[i];
            row.gameObject.SetActive(visible);
            if (!visible) continue;
            var standing = model.Standings[i];
            row.Render(i + 1, standing.Name, standing.Revenue, standing.Mine, standing.Team);
        }
        UIMatchHudScreen.SetText(dayClock, model.Timer?.Split('.')[0]);
        UIMatchHudScreen.SetText(dayCounter, model.DayCounter);
        UIMatchHudScreen.SetText(dayRevenue, model.DaySales.ToString("N0"));
        UIMatchHudScreen.SetText(dayGoal, model.DayBill.ToString("N0"));
        if (dayTimeGauge != null) dayTimeGauge.fillAmount = Mathf.Clamp01(model.DayTimeRatio);
        if (dayRentBadge != null) dayRentBadge.SetActive(model.RentMet);
        if (dayRevenue != null) dayRevenue.color = model.RentMet ? rentMetColor :
            model.DayRemaining <= rentWarningSeconds ? Color.Lerp(accent, alarmColor, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f)) : accent;
    }

    /// 낮이 끝나 `DayRoot`를 끄기 직전에 부른다. 꺼진 뒤에는 펼친 패널이 안 보이는데도
    /// 커서 잠금(`RecipeOpen`)만 남는다.
    public void Close()
    {
        recipeAvailable = false;
        if (RecipeOpen) ToggleRecipe(false);
    }

    /// 조합식 패널을 접거나 편다. <paramref name="force"/>를 주면 그 상태로 맞춘다.
    public void ToggleRecipe(bool? force = null)
    {
        SetRecipeOpen(recipeAvailable && (force ?? !RecipeOpen));
        // 펼친 동안은 탭을 누르고 스크롤해야 하므로 커서를 푼다 (`UIMatchHudScreen.WantsCursor`).
        UIManager.Instance.RefreshInputGates();
    }

    void SetRecipeOpen(bool open)
    {
        recipePanel.SetActive(open);
        if (recipeTabGlyph != null) recipeTabGlyph.text = "F1";
    }

    /// 켜진 보석만 세로로 쌓는다 (기획서 5.7.1: 낮 화면 상시 표시).
    public void RenderGems(Cafe cafe)
    {
        for (var i = 0; i < gemIcons.Length && i < Gems.All.Length; i++)
        {
            var gem = Gems.All[i];
            var turns = cafe != null ? cafe.GemTurns(gem) : 0;
            var slot = gemIcons[i];
            if (slot.gameObject.activeSelf != turns > 0) slot.gameObject.SetActive(turns > 0);
            if (turns > 0) slot.Render(ResourceManager.Instance.IngredientSprite(Gems.ItemOf(gem)), turns);
        }
    }

    public void RenderRecipes(TeamStock stock, System.Collections.Generic.IReadOnlyList<Ingredient> popular)
    {
        if (!RecipeOpen || recipeRows == null) return;
        foreach (var row in recipeRows) row.Render(stock, popular);
    }

    /// 매 프레임 불린다. 침은 초당 1.4회 왕복이라 HUD 갱신 주기(0.1초)로 옮기면 계단이 되고
    /// 판정 구간을 눈으로 노릴 수 없다 (기획서 5.2).
    public void SetCompletionGauge(in MatchHudPresenter.GaugeView view)
    {
        if (completionBar == null) return;

        UIMatchHudScreen.SetGroup(completionBar, view.Show);
        if (!view.Show) return;

        var width = completionBar.rect.width;
        SetZoneWidth(completionGoodZone, width * view.GoodHalf * 2f);
        SetZoneWidth(completionPerfectZone, width * view.PerfectHalf * 2f);

        if (completionNeedle != null)
            completionNeedle.anchoredPosition = new Vector2((view.Needle - 0.5f) * width, 0f);

        UIMatchHudScreen.SetText(completionLabel, view.Label);
    }

    /// 판정 구간의 폭. 값이 그대로면 손대지 않는다 — `sizeDelta` 대입은 매번 레이아웃을
    /// 더럽히는데 구간 폭은 게이지가 도는 내내 바뀌지 않는다.
    static void SetZoneWidth(RectTransform zone, float width)
    {
        if (zone == null || Mathf.Approximately(zone.sizeDelta.x, width)) return;
        zone.sizeDelta = new Vector2(width, zone.sizeDelta.y);
    }

    /// 매 프레임 불린다. 내 카드는 HUD에 고정, 팀원 카드는 식기를 따라다닌다.
    /// 내용은 손이 바뀔 때만 다시 그린다. 팀원 카드는 월드 표식 층에 올라가므로 화면을 받는다.
    public void SetMakingCards(in MatchHudPresenter.MakingCards cards, UIMatchHudScreen screen)
    {
        if (makingCardPrefab == null) return;
        if (!cards.ShowSelf && !cards.ShowMate && makingCards == null) return;

        if (makingCards == null)
        {
            if (makingCardDock == null)
                CDebug.LogError($"{name}: {nameof(makingCardDock)}가 비어 있다. 내 제조 카드가 화면 구석에 붙는다.", this);
            makingCards = new UIMakingCard[2];
            makingCards[0] = Instantiate(makingCardPrefab, makingCardDock != null ? makingCardDock : (RectTransform)transform);
            makingCards[1] = Instantiate(makingCardPrefab, screen.WorldMarkers);
        }

        UIMatchHudScreen.SetGroup(makingCards[0], cards.ShowSelf && makingCards[0].Render(cards.Self));
        var mate = makingCards[1];
        UIMatchHudScreen.SetGroup(mate, cards.ShowMate && cards.View != null && mate.Render(cards.Mate)
            && screen.PlaceMarker((RectTransform)mate.transform, cards.MateAt + Vector3.up * makingCardHeight, cards.View));
    }

    /// HUD가 내려갈 때 부른다. 카드는 HUD가 주인이라 같이 지운다.
    public void Unload()
    {
        if (makingCards != null)
            foreach (var card in makingCards) Destroy(card.gameObject);
        makingCards = null;
    }
}
