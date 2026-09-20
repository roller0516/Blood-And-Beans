using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 매치 중 상시 떠 있는 HUD. 날짜·페이즈·남은 시간·팀·가방·스킬을 자리별로 보여 준다.
///
/// 이 클래스는 값을 만들지 않는다. 무엇을 쓸지는 `MatchHudPresenter`가 정하고, 여기는
/// 받은 값을 정해진 자리에 그리기만 한다.
///
/// **트리는 프리팹에 있다.** 이 클래스는 아무것도 만들지 않고 이어 둔 참조에 값만 넣는다.
/// 예전에는 코드로 세웠지만, 그러면 자리·색·글꼴을 기획자가 손댈 수 없었다.
public sealed class UIMatchHudScreen : UIScreen
{
    /// 페이즈별 상세(예보·순위·접시·손님). 자리를 잡아 줄 수 없는 가변 길이라 오른쪽에
    /// 한 덩어리로 흘린다.
    [SerializeField] TMP_Text label;

    /// 루팅한 아이템이 빨려 들어갈 가방 아이콘. 프리팹에 이어 두지 않으면 아래에서
    /// 만들어 쓴다 — 이 아이콘은 연출의 목적지라서 없으면 획득 피드백이 사라진다.
    [SerializeField] RectTransform bagIcon;
    [SerializeField] UIBagStatus bagStatus;

    /// 가방 아이콘의 두 얼굴. 비어 있거나 꽉 찼으면 닫힌 쪽, 그 사이에는 열린 쪽이다.
    Sprite bagClosedSprite;
    Sprite bagOpenSprite;

    /// 가방이 열려 보이는 적재 구간의 위쪽 끝. 이 값을 넘으면 더 들어가지 않으므로 닫는다.
    ///
    /// ponytail: 기획서에 없는 값이다 — 6.5.5는 획득 연출(날아가기·스케일 펀치)까지만
    /// 정하고 아이콘이 열리고 닫히는 규칙은 없다. 100%가 아니라 겉보기가 갈리는
    /// 80%(`LoadBands.OverloadRatio`)에서 닫는 편이 맞다면 인스펙터에서 옮기면 된다.
    [SerializeField, Min(0f)] float bagOpenUntilRatio = 1f;
    
    /// 화면 중앙에서 내리는 양. 정확히 가운데는 캐릭터와 겹친다.
    [SerializeField] float castBarDrop = 90f;

    /// 월드 좌표를 따라가는 표식(주문 말풍선·재료 배지)이 올라가는 층. HUD 맨 아래에 깐다.
    /// ponytail: 표식끼리 앞뒤 정렬은 하지 않는다. 겹쳐 보이는 일이 생기면 카메라 거리로 정렬한다.
    [SerializeField] RectTransform worldMarkers;

    /// 표식 크기는 기준 깊이에서 1배, 멀수록 반비례로 줄고 범위 안에 묶인다.
    /// ponytail: 기획서에 없는 연출값이다. 플레이해 보며 인스펙터에서 맞춘다.
    [SerializeField, Min(0.01f)] float markerReferenceDepth = 5f;
    [SerializeField] Vector2 markerScaleRange = new(0.35f, 1f);

    [Header("조합식 — F1로 여닫는 중앙 패널")]
    /// 화면 중앙 패널. HUD 요소에 가리지 않게 `RecipeWrap`이 HUD 맨 위 자식이다.
    [SerializeField] GameObject recipePanel;
    [SerializeField] Button recipeTabButton;

    /// 펴져 있으면 ◀(접기), 접혀 있으면 ▶(펴기)다.
    [SerializeField] TMP_Text recipeTabGlyph;
    [SerializeField] UIRecipeRow recipeRowPrefab;

    /// 보석 칸. 순서는 `Gems.All`과 같다. 상한 6이 기획서로 고정이라 프리팹에 미리 깔아 둔다 (5.7.1).
    [SerializeField] UIGemIcon[] gemIcons = System.Array.Empty<UIGemIcon>();
    [SerializeField] RectTransform recipeContent;
    [SerializeField] RectTransform recipeDessertContent;
    UIRecipeRow[] recipeRows;
    bool recipeAvailable;
    public bool RecipeOpen => recipePanel != null && recipePanel.activeSelf;

    [Header("귀환 경보")]
    // 밤 마감 30초 전에 울리는 종소리 (기획서 6.4). 애셋이 아직 없어서 비워 둘 수 있고,
    // 비어 있으면 화살표만 뜬다 — 소리는 이 표시의 필수 조건이 아니다.
    [SerializeField] AudioClip returnAlarmSound;

    [SerializeField] Color alarmColor = new(0.93f, 0.35f, 0.28f);

    /// 마커가 화면 밖으로 나갔을 때 가장자리에서 띄워 두는 거리. 0이면 마커의 절반이
    /// 화면 밖으로 잘려 나간다.
    [SerializeField] Vector2 markerEdgeMargin = new(96f, 72f);

    [Header("색")]
    [SerializeField] Color accent = new(0.91f, 0.77f, 0.42f);       // 금색 강조
    [SerializeField] Color muted = new(0.72f, 0.72f, 0.68f, 0.9f);  // 라벨
    [SerializeField] Color panelBack = new(0.04f, 0.05f, 0.05f, 0.55f);

    [Header("여백")]
    [SerializeField] float margin = 24f;


    [Header("왼쪽 위 — 매출·가방")]
    [SerializeField] TMP_Text revenueText;
    [SerializeField] TMP_Text bagPercentText;
    [SerializeField] TMP_Text bagWeightText;
    [SerializeField] RectTransform bagFill;

    /// 가방을 묻어 두면 게이지와 글자가 이 색으로 바뀐다. 글자만으로는 눈에 안 들어오는데,
    /// 묻힌 동안에는 담기가 전부 거절되므로 한눈에 보여야 한다 (기획서 6.7 묻기).
    [SerializeField] Color buriedColor = new(0.85f, 0.34f, 0.25f);

    /// 묻어 둔 동안 가방 아이콘의 투명도. 아이콘에는 색을 씌우지 않고 이것만 낮춘다.
    [SerializeField, Range(0f, 1f)] float bagBuriedAlpha = 0.35f;

    /// 무게 구간별 게이지 색 (기획서 6.7: "구간이 바뀔 때 색과 발소리가 바뀐다").
    /// 인덱스는 `LoadBands`의 밴드와 같다 — 0~50% / 50~80% / 80~100% / 100~130% /
    /// 130~160% / 160~200% / 200%~. 80%를 넘는 순간부터 경고색으로 넘어간다.
    [SerializeField] Color[] bagBandColors =
    {
        new(0.55f, 0.78f, 0.45f),   // 0~50%   가볍다
        new(0.80f, 0.80f, 0.42f),   // 50~80%  느려지기 시작
        new(0.93f, 0.66f, 0.28f),   // 80~100% 대시에 맞으면 흘린다
        new(0.93f, 0.42f, 0.25f),   // 100~130% 화면이 흔들린다
        new(0.85f, 0.28f, 0.28f),   // 130~160%
        new(0.70f, 0.20f, 0.30f),   // 160~200%
        new(0.52f, 0.14f, 0.32f),   // 200%~   사실상 정지
    };

    /// 매 갱신마다 찾지 않으려고 캐시한다. HUD 갱신은 0.1초마다 도는 주기 실행이라
    /// 여기서 `GetComponent`를 부르면 그것이 곧 주기 실행 안의 컴포넌트 조회다 (AGENTS.md).
    Image bagFillImage;
    Image bagIconImage;

    [Header("위 가운데 — 날짜·페이즈·시계")]
    [SerializeField] TMP_Text dayText;
    [SerializeField] TMP_Text phaseText;
    [SerializeField] TMP_Text timerText;

    [Header("오른쪽 위")]
    [SerializeField] TMP_Text teamText;

    /// 낮·밤 헤더와 따로 둔다. 헤더는 페이즈마다 꺼지지만 핑은 늘 보여야 한다.
    [SerializeField] TMP_Text pingText;

    [Header("상호작용 안내")]
    [SerializeField] RectTransform promptBox;
    [SerializeField] TMP_Text promptText;

    [Header("귀환 표시")]
    [SerializeField] RectTransform returnBox;
    [SerializeField] RectTransform returnArrow;
    [SerializeField] TMP_Text returnLabel;

    [Header("개봉 게이지")]
    [SerializeField] RectTransform castBar;
    [SerializeField] RectTransform castFill;

    [Header("완성 게이지")]
    /// 기획서 5.2의 침 게이지. 낮에만 뜬다. 구간 폭은 `CompletionGauge`가 판정에 쓰는
    /// 값을 그대로 받아 그린다 — 여기 따로 적으면 보이는 구간과 맞는 구간이 어긋난다.
    [SerializeField] RectTransform completionBar;
    [SerializeField] RectTransform completionGoodZone;
    [SerializeField] RectTransform completionPerfectZone;
    [SerializeField] RectTransform completionNeedle;
    [SerializeField] TMP_Text completionLabel;

    [Header("대시")]
    [SerializeField] RectTransform dashSlot;
    [SerializeField] TMP_Text dashLabelText;
    [SerializeField] TMP_Text dashTimeText;
    [SerializeField] RectTransform dashFill;

    /// 이번 경보의 종을 이미 울렸는가. 경보가 꺼지면 풀려서 다음 밤에 다시 울린다.
    bool alarmRung;
    [Header("낮 상단 띠")]
    [SerializeField] GameObject dayHeader;
    [SerializeField] TMP_Text dayRanking;
    [SerializeField] TMP_Text dayClock;
    [SerializeField] TMP_Text dayRevenue;
    [SerializeField] GameObject[] nightHeader;
    [SerializeField] Color rentMetColor = new(0.45f, 0.85f, 0.5f);
    // ponytail: 경고 시점은 PDF 목업의 30초. 밸런스 확정 시 인스펙터에서 조정한다.
    [SerializeField] float rentWarningSeconds = 30f;

    /// 가방 연출의 목적지. 절대 null이 아니다.
    public RectTransform BagAnchor => bagIcon;

    /// HUD는 누르는 곳이 없다. 밤에는 마우스가 카메라를 돌리므로 커서를 잠근다.
    public override bool WantsCursor => recipePanel != null && recipePanel.activeSelf;

    protected override void Awake()
    {
        base.Awake();

        // 트리는 프리팹에 있다. 여기서 만들지 않는다 — 만들면 프리팹에서 고친 자리가
        // 매번 덮인다.
        if (bagFill != null) bagFillImage = bagFill.GetComponent<Image>();
        if (bagIcon != null) bagIconImage = bagIcon.GetComponent<Image>();

        var resources = ResourceManager.Instance;
        bagClosedSprite = resources.GetSprite(ResourceManager.BagClosed);
        bagOpenSprite = resources.GetSprite(ResourceManager.BagOpen);

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

    /// 조합식 패널을 접거나 편다. <paramref name="force"/>를 주면 그 상태로 맞춘다.
    public void ToggleRecipe(bool? force = null)
    {
        SetRecipeOpen(recipeAvailable && (force ?? !RecipeOpen));
        // 펼친 동안은 탭을 누르고 스크롤해야 하므로 커서를 푼다 (`WantsCursor`).
        UIManager.Instance.RefreshInputGates();
    }

    void SetRecipeOpen(bool open)
    {
        recipePanel.SetActive(open);
        if (recipeTabGlyph != null) recipeTabGlyph.text = "F1";
    }

    /// 켜진 보석만 세로로 쌓는다. `cafe`가 null이면(밤·전환) 전부 끈다 (기획서 5.7.1: 낮 화면 상시 표시).
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

    /// 표식은 진열대·손님이 주인이다(주인이 자기 OnDestroy에서 지운다). HUD가 내려갈 때 같이 파괴되지
    /// 않게 층 밖으로 내보낸다. HUD가 다시 뜨면 `PlaceMarker`가 다시 데려온다.
    public override void OnUnload()
    {
        for (var i = worldMarkers.childCount - 1; i >= 0; i--) worldMarkers.GetChild(i).SetParent(null, false);
        markerDepth.Clear();
    }

    /// 표식마다 마지막으로 잰 카메라 깊이. 가까운 표식이 위에 그려지도록 형제 순서를 맞추는 데 쓴다.
    readonly System.Collections.Generic.Dictionary<Transform, float> markerDepth = new();

    /// 표식을 이 층으로 옮기고 월드 좌표 위에 놓는다. 카메라 뒤면 false다.
    public bool PlaceMarker(RectTransform marker, Vector3 world, Camera view)
    {
        if (marker.parent != worldMarkers) marker.SetParent(worldMarkers, false);

        var screenPoint = view.WorldToScreenPoint(world);
        if (screenPoint.z <= 0f) return false;

        markerDepth[marker] = screenPoint.z;
        SortMarker(marker, screenPoint.z);

        // Overlay 캔버스라 변환에 카메라를 넘기지 않는다.
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(worldMarkers, screenPoint, null, out var local))
            return false;
        if (marker.anchoredPosition != local) marker.anchoredPosition = local;

        // 오버레이는 원근이 없어서 멀리 있는 표식도 같은 픽셀로 그려진다. 깊이로 줄인다.
        var scale = Mathf.Clamp(markerReferenceDepth / screenPoint.z, markerScaleRange.x, markerScaleRange.y);
        if (!Mathf.Approximately(marker.localScale.x, scale)) marker.localScale = new Vector3(scale, scale, 1f);
        return true;
    }

    /// 먼 표식이 앞(먼저 그려짐), 가까운 표식이 뒤에 오게 이웃과 비교해 한 칸씩 옮긴다.
    /// 표식은 매 프레임 조금씩만 움직이므로 전체 정렬 없이 이것으로 순서가 유지된다.
    void SortMarker(Transform marker, float depth)
    {
        var index = marker.GetSiblingIndex();
        var target = index;
        while (target > 0 && DepthOf(worldMarkers.GetChild(target - 1)) < depth) target--;
        while (target < worldMarkers.childCount - 1 && DepthOf(worldMarkers.GetChild(target + 1)) > depth) target++;
        if (target != index) marker.SetSiblingIndex(target);
    }

    float DepthOf(Transform marker) => markerDepth.TryGetValue(marker, out var depth) ? depth : float.MaxValue;

    /// 무게 구간에 맞는 색. 표가 비어 있으면 기존 강조색으로 떨어진다 — 색이 없다고
    /// 게이지가 사라지면 안 된다.
    Color BandColor(int band)
    {
        if (bagBandColors == null || bagBandColors.Length == 0) return accent;
        return bagBandColors[Mathf.Clamp(band, 0, bagBandColors.Length - 1)];
    }

    /// 한 번에 한 덩어리로 받는다. 칸마다 따로 부르면 어느 칸이 이번 갱신에 빠졌는지
    /// 부르는 쪽이 기억해야 한다.
    public void Render(in MatchHudModel model)
    {
        recipeAvailable = model.IsDay;
        if (recipeTabButton != null) recipeTabButton.gameObject.SetActive(recipeAvailable);
        if (!recipeAvailable && RecipeOpen) ToggleRecipe(false);
        if (dayHeader != null) dayHeader.SetActive(model.IsDay);
        if (nightHeader != null) foreach (var part in nightHeader) if (part != null) part.SetActive(!model.IsDay);
        if (model.IsDay)
        {
            SetText(dayRanking, model.Ranking);
            SetText(dayClock, $"{model.Day}   {model.Timer.Split('.')[0]}");
            SetText(dayRevenue, model.Revenue);
            if (dayRevenue != null) dayRevenue.color = model.RentMet ? rentMetColor :
                model.DayRemaining <= rentWarningSeconds ? Color.Lerp(accent, alarmColor, 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 5f)) : accent;
        }
        SetText(dayText, model.Day);
        SetText(phaseText, model.PhaseName);
        SetText(timerText, model.Timer);
        SetText(teamText, model.Team);
        SetText(pingText, model.Ping);
        SetText(revenueText, model.Revenue);
        SetText(label, model.Details);

        SetGroup(bagPercentText, model.ShowBag);
        SetGroup(bagWeightText, model.ShowBag);
        // 가방 아이콘은 밤의 획득 연출 목표다 (기획서 6.5.5). 낮에도 켜 두면 정체를 알 수
        // 없는 사각형이 남는다.
        SetGroup(bagIcon, model.ShowBag);
        if (bagStatus != null) bagStatus.SetMissing(model.ShowBag && model.BagBuried);
        if (bagFill != null)
        {
            SetGroup(bagFill, model.ShowBag);
            if (model.ShowBag)
            {
                SetText(bagPercentText, model.BagPercent);
                SetText(bagWeightText, model.BagWeight);
                bagFill.localScale = new Vector3(Mathf.Clamp01(model.BagRatio), 1f, 1f);

                // 묻어 둔 동안에는 적재량이 의미가 없다. 게이지를 비우고 색으로 알린다.
                // 메고 있으면 무게 구간이 색을 정한다 (기획서 6.7).
                var tint = model.BagBuried ? buriedColor : BandColor(model.BagBand);
                if (bagFillImage != null) bagFillImage.color = tint; 
                if (bagPercentText != null)
                    bagPercentText.color = model.BagBuried ? buriedColor : tint;
                if (bagIconImage != null)
                {
                    // 아이콘은 그림 색 그대로 둔다. 색을 곱하면 열린 가방과 닫힌 가방이
                    // 둘 다 그 색으로 뭉개진다. 묻어 둔 것은 흐리게 해서만 알린다 —
                    // X 표시는 `UIBagStatus`가 따로 그린다.
                    bagIconImage.color = new Color(1f, 1f, 1f, model.BagBuried ? bagBuriedAlpha : 1f);

                    // 하나라도 담겨 있으면 열어 둔다. 묻어 둔 가방은 적재가 0으로 와서
                    // 저절로 닫힌다 (`MatchHudPresenter`).
                    var open = model.BagRatio > 0f && model.BagRatio < bagOpenUntilRatio;
                    var face = open ? bagOpenSprite : bagClosedSprite;
                    if (face != null && bagIconImage.sprite != face) bagIconImage.sprite = face;
                }
            }
        }

        var hasPrompt = !string.IsNullOrEmpty(model.Prompt);
        SetGroup(promptBox, hasPrompt);
        if (hasPrompt) SetText(promptText, model.Prompt);

        SetGroup(dashSlot, model.ShowDash);
        if (model.ShowDash)
        {
            SetText(dashTimeText, model.DashTime);
            dashLabelText.color = model.DashReady ? accent : muted;
            dashFill.localScale = new Vector3(Mathf.Clamp01(model.DashRatio), 1f, 1f);
        }
    }

    /// 0이면 감추고, 그 위면 그만큼 채운다. 매 프레임 불러도 된다 — 켜고 끄는 것은
    /// 상태가 바뀔 때만이고 나머지는 스케일 대입 하나다.
    public void SetCastProgress(float ratio01)
    {
        if (castBar == null) return;

        var active = ratio01 > 0f;
        if (castBar.gameObject.activeSelf != active) castBar.gameObject.SetActive(active);
        if (!active) return;

        // 폭을 sizeDelta로 줄이지 않고 스케일로 민다. 앵커 스트레치라 sizeDelta는 여백이
        // 되어 오른쪽부터 줄어든다 — 게이지는 왼쪽에서 자라야 한다.
        castFill.localScale = new Vector3(Mathf.Clamp01(ratio01), 1f, 1f);
    }

    /// 매 프레임 불린다. 침은 초당 1.4회 왕복이라 HUD 갱신 주기(0.1초)로 옮기면 계단이 되고
    /// 판정 구간을 눈으로 노릴 수 없다 — 개봉 게이지와 같은 이유다 (기획서 5.2).
    public void SetCompletionGauge(in MatchHudPresenter.GaugeView view)
    {
        if (completionBar == null) return;

        SetGroup(completionBar, view.Show);
        if (!view.Show) return;

        var width = completionBar.rect.width;
        SetZoneWidth(completionGoodZone, width * view.GoodHalf * 2f);
        SetZoneWidth(completionPerfectZone, width * view.PerfectHalf * 2f);

        if (completionNeedle != null)
            completionNeedle.anchoredPosition = new Vector2((view.Needle - 0.5f) * width, 0f);

        SetText(completionLabel, view.Label);
    }

    /// 판정 구간의 폭. 값이 그대로면 손대지 않는다 — `sizeDelta` 대입은 매번 레이아웃을
    /// 더럽히는데 구간 폭은 게이지가 도는 내내 바뀌지 않는다.
    static void SetZoneWidth(RectTransform zone, float width)
    {
        if (zone == null || Mathf.Approximately(zone.sizeDelta.x, width)) return;
        zone.sizeDelta = new Vector2(width, zone.sizeDelta.y);
    }

    static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value ?? string.Empty;
    }

    void SetGroup(Component target, bool active)
    {
        if (target != null && target.gameObject.activeSelf != active) target.gameObject.SetActive(active);
    }

    /// 매 프레임 불린다. 마커는 월드의 한 점에 붙어 있어서 HUD 갱신 주기(0.1초)로 옮기면
    /// 카메라가 도는 동안 계단처럼 끊긴다 — 개봉 게이지와 같은 이유다.
    ///
    /// 화면 안이면 카페 위에 그대로 뜨고 화살표는 감춘다. 눈에 보이는 것을 두고 방향까지
    /// 가리킬 이유가 없다. 화면 밖이면 가장자리에 붙고 화살표가 그쪽을 가리킨다.
    public void SetReturnMarker(in MatchHudPresenter.ReturnMarker marker)
    {
        if (returnBox == null) return;

        SetGroup(returnBox, marker.Show);
        if (!marker.Show)
        {
            alarmRung = false;      // 다음 밤에 다시 울린다
            return;
        }

        var half = ((RectTransform)transform).rect.size * 0.5f;
        var point = new Vector2((marker.Viewport.x - 0.5f) * half.x * 2f,
                                (marker.Viewport.y - 0.5f) * half.y * 2f);

        if (marker.Offscreen)
        {
            point = ClampToEdge(point, half - markerEdgeMargin);
            returnArrow.localEulerAngles = new Vector3(0f, 0f, marker.Angle);
        }

        returnBox.anchoredPosition = point;
        SetGroup(returnArrow, marker.Offscreen);
        SetText(returnLabel, marker.Label);
        RingOnce();
    }

    /// 중앙에서 `point`로 향하는 방향은 유지한 채 `half` 사각형 안으로 끌어당긴다.
    /// 성분별로 자르면 방향이 꺾여서 화살표와 마커가 서로 다른 곳을 가리킨다.
    static Vector2 ClampToEdge(Vector2 point, Vector2 half)
    {
        var scaleX = Mathf.Abs(point.x) > 0.0001f ? half.x / Mathf.Abs(point.x) : float.MaxValue;
        var scaleY = Mathf.Abs(point.y) > 0.0001f ? half.y / Mathf.Abs(point.y) : float.MaxValue;
        return point * Mathf.Min(1f, Mathf.Min(scaleX, scaleY));
    }

    /// 경보 구간에 들어간 순간 한 번만 울린다. `Camera.main`은 여기서만 쓴다 — 밤 한 번에
    /// 한 번 도는 경로라 주기 실행이 아니다 (AGENTS.md).
    void RingOnce()
    {
        if (alarmRung) return;
        alarmRung = true;

        if (returnAlarmSound == null) return;
        var listener = Camera.main;
        AudioSource.PlayClipAtPoint(returnAlarmSound,
            listener != null ? listener.transform.position : Vector3.zero);
    }
}

/// HUD 한 갱신분의 값. 화면이 그리기만 하도록 자리별로 나눠 담는다 — 한 덩어리 문자열로
/// 넘기면 어느 칸에 무엇이 들어갈지를 화면이 다시 파싱해야 한다.
public struct MatchHudModel
{
    public bool IsDay;
    public bool RentMet;
    public float DayRemaining;
    public string Ranking;
    public string Day;          // "2일차"
    public string PhaseName;    // "야간 탐색"
    public string Timer;        // "02:46.021"
    public string Team;         // "Team 0"
    public string Ping;         // "핑 42ms" / "호스트"
    public string Revenue;      // "팀 매출  2,840G"
    public string Details;      // 예보·순위·접시·손님
    public string Prompt;       // "[F] 상자 열기"

    public bool ShowBag;
    public string BagPercent;   // "가방 용량  42%"
    public string BagWeight;    // "3.4 / 8.0 KG"
    public float BagRatio;

    /// 무게 구간 (`LoadBands.BandOf`). 게이지 색이 이 값으로 갈린다 (기획서 6.7).
    public int BagBand;

    /// 가방을 땅에 묻어 뒀는가 (기획서 6.7). 묻힌 동안 담기가 전부 거절되므로 글자뿐
    /// 아니라 색으로도 구분한다.
    public bool BagBuried;

    public bool ShowDash;
    public bool DashReady;
    public string DashTime;     // "6.0s" / "과적"
    public float DashRatio;     // 남은 쿨다운 비율
}
