using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 매치 중 상시 떠 있는 HUD. 날짜·페이즈·남은 시간·팀·가방·스킬을 자리별로 보여 준다.
///
/// 이 클래스는 값을 만들지 않는다. 무엇을 쓸지는 `MatchHudPresenter`가 정하고, 여기는
/// 받은 값을 정해진 자리에 그리기만 한다.
///
/// 위젯을 프리팹이 아니라 코드로 세운다. 프리팹에는 상세 글자 하나만 있고, 나머지는
/// 자리와 크기가 서로 물려 있어 Inspector로 흩어 두면 한 칸만 어긋나도 배치가 무너진다.
public sealed class MatchHudScreen : UIScreen
{
    /// 페이즈별 상세(예보·순위·접시·손님). 자리를 잡아 줄 수 없는 가변 길이라 오른쪽에
    /// 한 덩어리로 흘린다.
    [SerializeField] TMP_Text label;

    /// 루팅한 아이템이 빨려 들어갈 가방 아이콘. 프리팹에 이어 두지 않으면 아래에서
    /// 만들어 쓴다 — 이 아이콘은 연출의 목적지라서 없으면 획득 피드백이 사라진다.
    [SerializeField] RectTransform bagIcon;

    /// 프리팹에 아이콘이 없을 때 만들 자리와 크기. 화면 오른쪽 아래 모서리 기준이다.
    [SerializeField] Vector2 bagIconSize = new(72f, 72f);
    [SerializeField] Vector2 bagIconMargin = new(-72f, 72f);

    [Header("개봉 게이지")]
    /// 상자 개봉 로딩 바. 화면 한가운데에 뜬다 — 이건 지금 무엇을 기다리는지 알려 주는
    /// 값이라 HUD 글자 덩어리에 섞으면 시선이 닿지 않는다.
    [SerializeField] Vector2 castBarSize = new(260f, 14f);

    /// 화면 중앙에서 내리는 양. 정확히 가운데는 캐릭터와 겹친다.
    [SerializeField] float castBarDrop = 90f;

    [SerializeField] Color castBarBack = new(0f, 0f, 0f, 0.55f);
    [SerializeField] Color castBarFill = new(1f, 0.82f, 0.29f);

    [Header("귀환 경보")]
    /// 밤 마감 30초 전에 울리는 종소리 (기획서 6.4). 애셋이 아직 없어서 비워 둘 수 있고,
    /// 비어 있으면 화살표만 뜬다 — 소리는 이 표시의 필수 조건이 아니다.
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

    RectTransform castBar;
    RectTransform castFill;

    TMP_Text revenueText;
    TMP_Text bagPercentText;
    TMP_Text bagWeightText;
    RectTransform bagFill;

    TMP_Text dayText;
    TMP_Text phaseText;
    TMP_Text timerText;
    TMP_Text teamText;

    RectTransform promptBox;
    TMP_Text promptText;

    RectTransform returnBox;
    RectTransform returnArrow;
    TMP_Text returnLabel;

    /// 이번 경보의 종을 이미 울렸는가. 경보가 꺼지면 풀려서 다음 밤에 다시 울린다.
    bool alarmRung;

    RectTransform dashSlot;
    TMP_Text dashLabelText;
    TMP_Text dashTimeText;
    RectTransform dashFill;

    /// 가방 연출의 목적지. 절대 null이 아니다.
    public RectTransform BagAnchor => bagIcon;

    /// HUD는 누르는 곳이 없다. 밤에는 마우스가 카메라를 돌리므로 커서를 잠근다.
    public override bool WantsCursor => false;

    protected override void Awake()
    {
        base.Awake();
        if (bagIcon == null) bagIcon = BuildBagIcon();
        BuildCastBar();
        BuildTopLeft();
        BuildTopCenter();
        BuildTopRight();
        BuildPrompt();
        BuildDashSlot();
        BuildReturnIndicator();
        PlaceDetailLabel();
    }

    /// 한 번에 한 덩어리로 받는다. 칸마다 따로 부르면 어느 칸이 이번 갱신에 빠졌는지
    /// 부르는 쪽이 기억해야 한다.
    public void Render(in MatchHudModel model)
    {
        SetText(dayText, model.Day);
        SetText(phaseText, model.PhaseName);
        SetText(timerText, model.Timer);
        SetText(teamText, model.Team);
        SetText(revenueText, model.Revenue);
        SetText(label, model.Details);

        SetGroup(bagPercentText, model.ShowBag);
        SetGroup(bagWeightText, model.ShowBag);
        if (bagFill != null)
        {
            SetGroup(bagFill, model.ShowBag);
            if (model.ShowBag)
            {
                SetText(bagPercentText, model.BagPercent);
                SetText(bagWeightText, model.BagWeight);
                bagFill.localScale = new Vector3(Mathf.Clamp01(model.BagRatio), 1f, 1f);
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

    static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value ?? string.Empty;
    }

    static void SetGroup(Component target, bool active)
    {
        if (target != null && target.gameObject.activeSelf != active) target.gameObject.SetActive(active);
    }

    // 화면 왼쪽 위: 팀 매출과 가방 적재량.
    void BuildTopLeft()
    {
        var root = MakePanel("TopLeft", new Vector2(0f, 1f), new Vector2(margin, -margin), new Vector2(300f, 92f));

        revenueText = MakeText(root, "Revenue", new Vector2(0f, 1f), new Vector2(12f, -8f),
                               new Vector2(276f, 24f), 20f, accent, TextAlignmentOptions.Left);
        bagPercentText = MakeText(root, "BagPercent", new Vector2(0f, 1f), new Vector2(12f, -34f),
                                  new Vector2(276f, 22f), 17f, muted, TextAlignmentOptions.Left);
        bagFill = MakeBar(root, "BagBar", new Vector2(0f, 1f), new Vector2(12f, -58f),
                          new Vector2(276f, 6f), accent);
        bagWeightText = MakeText(root, "BagWeight", new Vector2(0f, 1f), new Vector2(12f, -66f),
                                 new Vector2(276f, 20f), 14f, muted, TextAlignmentOptions.Left);
    }

    // 화면 위 가운데: 날짜 · 페이즈 · 남은 시간.
    void BuildTopCenter()
    {
        var root = MakePanel("TopCenter", new Vector2(0.5f, 1f), new Vector2(0f, -margin), new Vector2(420f, 40f));

        dayText = MakeText(root, "Day", new Vector2(0f, 0.5f), new Vector2(16f, 0f),
                           new Vector2(90f, 28f), 18f, accent, TextAlignmentOptions.Left);
        phaseText = MakeText(root, "Phase", new Vector2(0f, 0.5f), new Vector2(118f, 0f),
                             new Vector2(140f, 28f), 17f, muted, TextAlignmentOptions.Left);
        timerText = MakeText(root, "Timer", new Vector2(1f, 0.5f), new Vector2(-16f, 0f),
                             new Vector2(150f, 30f), 22f, Color.white, TextAlignmentOptions.Right);
    }

    // 화면 오른쪽 위: 지금 이 화면이 어느 팀인가.
    void BuildTopRight()
    {
        teamText = MakeText(transform, "Team", new Vector2(1f, 1f), new Vector2(-margin, -margin),
                            new Vector2(220f, 26f), 18f, Color.white, TextAlignmentOptions.Right);
    }

    // 상호작용 안내. 캐릭터 바로 아래에 뜬다 — 오른쪽 글자 덩어리에 섞으면 눈이 가지 않는다.
    void BuildPrompt()
    {
        promptBox = MakePanel("Prompt", new Vector2(0.5f, 0.5f), new Vector2(0f, -castBarDrop - 60f),
                              new Vector2(220f, 40f));

        promptText = MakeText(promptBox, "Text", new Vector2(0.5f, 0.5f), Vector2.zero,
                              new Vector2(200f, 30f), 17f, Color.white, TextAlignmentOptions.Center);
        promptBox.gameObject.SetActive(false);
    }

    // 화면 왼쪽 아래: 지금 존재하는 유일한 행동인 대시.
    void BuildDashSlot()
    {
        dashSlot = MakePanel("DashSlot", new Vector2(0f, 0f), new Vector2(margin, margin),
                             new Vector2(200f, 48f));

        dashLabelText = MakeText(dashSlot, "Name", new Vector2(0f, 1f), new Vector2(12f, -6f),
                                 new Vector2(120f, 22f), 16f, accent, TextAlignmentOptions.Left);
        dashLabelText.text = "대시";
        dashTimeText = MakeText(dashSlot, "Time", new Vector2(1f, 1f), new Vector2(-12f, -6f),
                                new Vector2(80f, 22f), 14f, muted, TextAlignmentOptions.Right);
        dashFill = MakeBar(dashSlot, "Bar", new Vector2(0f, 0f), new Vector2(12f, 10f),
                           new Vector2(176f, 5f), accent);

        dashSlot.gameObject.SetActive(false);
    }

    // 화면 위 가운데, 시계 바로 아래. 밤이 끝나기 직전에만 뜬다 (기획서 6.4).
    // 화살표는 카메라 기준이라 쿼터뷰에서도 화면에서 보이는 그 방향이 곧 갈 방향이다.
    void BuildReturnIndicator()
    {
        // 배경판이 없다. 월드 위에 떠 있는 표시라 판을 깔면 그 뒤의 숲이 잘려 보인다 —
        // 화살표와 글자만 남긴다. 그래서 `MakePanel`(Image를 붙인다)을 쓰지 않는다.
        //
        // 앵커는 화면 중앙이다. 월드의 한 점을 따라다니므로 고정된 자리가 없고,
        // `SetReturnMarker`가 중앙 기준 오프셋으로 옮긴다.
        var root = new GameObject("ReturnMarker", typeof(RectTransform));
        returnBox = (RectTransform)root.transform;
        returnBox.SetParent(transform, false);
        returnBox.anchorMin = returnBox.anchorMax = returnBox.pivot = new Vector2(0.5f, 0.5f);
        returnBox.sizeDelta = new Vector2(168f, 92f);
        returnBox.anchoredPosition = Vector2.zero;

        var arrow = MakeText(returnBox, "Arrow", new Vector2(0.5f, 1f), new Vector2(0f, -8f),
                             new Vector2(56f, 56f), 44f, alarmColor, TextAlignmentOptions.Center);
        arrow.text = "▲";      // Pretendard에 있는 글리프다. 화살표 스프라이트가 없다
        returnArrow = (RectTransform)arrow.transform;

        returnLabel = MakeText(returnBox, "Label", new Vector2(0.5f, 0f), new Vector2(0f, 12f),
                               new Vector2(168f, 24f), 17f, alarmColor, TextAlignmentOptions.Center);

        returnBox.gameObject.SetActive(false);
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

    // 프리팹의 상세 글자는 화면 전체를 덮게 늘어나 있다. 다른 칸과 겹치지 않게 오른쪽
    // 열로 접어 둔다.
    void PlaceDetailLabel()
    {
        if (label == null) return;

        var rect = (RectTransform)label.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(360f, 420f);
        rect.anchoredPosition = new Vector2(-margin, -(margin + 44f));
        label.alignment = TextAlignmentOptions.TopRight;
        label.raycastTarget = false;
    }

    RectTransform MakePanel(string name, Vector2 anchor, Vector2 position, Vector2 size)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var image = go.GetComponent<Image>();
        image.color = panelBack;
        image.raycastTarget = false;
        return rect;
    }

    TMP_Text MakeText(Transform parent, string name, Vector2 anchor, Vector2 position,
                      Vector2 size, float fontSize, Color color, TextAlignmentOptions align)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = align;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    /// 왼쪽에서 자라는 막대. 반환값은 채워지는 쪽이며 `localScale.x`로 진행도를 준다.
    RectTransform MakeBar(Transform parent, string name, Vector2 anchor, Vector2 position,
                          Vector2 size, Color fillColor)
    {
        var back = new GameObject(name, typeof(RectTransform), typeof(Image));
        var backRect = (RectTransform)back.transform;
        backRect.SetParent(parent, false);
        backRect.anchorMin = backRect.anchorMax = backRect.pivot = anchor;
        backRect.sizeDelta = size;
        backRect.anchoredPosition = position;

        var backImage = back.GetComponent<Image>();
        backImage.color = castBarBack;
        backImage.raycastTarget = false;

        var front = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        var fill = (RectTransform)front.transform;
        fill.SetParent(backRect, false);
        fill.anchorMin = Vector2.zero;
        fill.anchorMax = Vector2.one;
        fill.pivot = new Vector2(0f, 0.5f);
        fill.offsetMin = fill.offsetMax = Vector2.zero;

        var frontImage = front.GetComponent<Image>();
        frontImage.color = fillColor;
        frontImage.raycastTarget = false;
        return fill;
    }

    void BuildCastBar()
    {
        var back = new GameObject("CastBar", typeof(RectTransform), typeof(Image));
        castBar = (RectTransform)back.transform;
        castBar.SetParent(transform, false);
        castBar.anchorMin = castBar.anchorMax = castBar.pivot = new Vector2(0.5f, 0.5f);
        castBar.sizeDelta = castBarSize;
        castBar.anchoredPosition = new Vector2(0f, -castBarDrop);

        var backImage = back.GetComponent<Image>();
        backImage.color = castBarBack;
        backImage.raycastTarget = false;

        var front = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        castFill = (RectTransform)front.transform;
        castFill.SetParent(castBar, false);
        castFill.anchorMin = new Vector2(0f, 0f);
        castFill.anchorMax = new Vector2(1f, 1f);
        castFill.pivot = new Vector2(0f, 0.5f);      // 왼쪽에서 자란다
        castFill.offsetMin = castFill.offsetMax = Vector2.zero;

        var frontImage = front.GetComponent<Image>();
        frontImage.color = castBarFill;
        frontImage.raycastTarget = false;

        castBar.gameObject.SetActive(false);
    }

    RectTransform BuildBagIcon()
    {
        var go = new GameObject("BagIcon", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0f);
        rect.sizeDelta = bagIconSize;
        rect.anchoredPosition = bagIconMargin;

        var image = go.GetComponent<Image>();
        image.color = new Color(0.35f, 0.26f, 0.18f, 0.9f);
        image.raycastTarget = false;
        return rect;
    }
}

/// HUD 한 갱신분의 값. 화면이 그리기만 하도록 자리별로 나눠 담는다 — 한 덩어리 문자열로
/// 넘기면 어느 칸에 무엇이 들어갈지를 화면이 다시 파싱해야 한다.
public struct MatchHudModel
{
    public string Day;          // "2일차"
    public string PhaseName;    // "야간 탐색"
    public string Timer;        // "02:46.021"
    public string Team;         // "Team 0"
    public string Revenue;      // "팀 매출  2,840G"
    public string Details;      // 예보·순위·접시·손님
    public string Prompt;       // "[F] 상자 열기"

    public bool ShowBag;
    public string BagPercent;   // "가방 용량  42%"
    public string BagWeight;    // "3.4 / 8.0 KG"
    public float BagRatio;

    public bool ShowDash;
    public bool DashReady;
    public string DashTime;     // "6.0s" / "과적"
    public float DashRatio;     // 남은 쿨다운 비율
}
