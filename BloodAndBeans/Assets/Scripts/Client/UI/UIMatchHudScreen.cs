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


    [Header("왼쪽 위 — 매출·가방")]
    [SerializeField] TMP_Text revenueText;
    [SerializeField] TMP_Text bagPercentText;
    [SerializeField] TMP_Text bagWeightText;
    [SerializeField] RectTransform bagFill;

    /// 가방을 묻어 두면 게이지와 아이콘이 이 색으로 바뀐다. 글자만으로는 눈에 안 들어오는데,
    /// 묻힌 동안에는 담기가 전부 거절되므로 한눈에 보여야 한다 (기획서 6.7 묻기).
    [SerializeField] Color buriedColor = new(0.85f, 0.34f, 0.25f);

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

    [Header("대시")]
    [SerializeField] RectTransform dashSlot;
    [SerializeField] TMP_Text dashLabelText;
    [SerializeField] TMP_Text dashTimeText;
    [SerializeField] RectTransform dashFill;

    /// 이번 경보의 종을 이미 울렸는가. 경보가 꺼지면 풀려서 다음 밤에 다시 울린다.
    bool alarmRung;

    /// 가방 연출의 목적지. 절대 null이 아니다.
    public RectTransform BagAnchor => bagIcon;

    /// HUD는 누르는 곳이 없다. 밤에는 마우스가 카메라를 돌리므로 커서를 잠근다.
    public override bool WantsCursor => false;

    protected override void Awake()
    {
        base.Awake();

        // 트리는 프리팹에 있다. 여기서 만들지 않는다 — 만들면 프리팹에서 고친 자리가
        // 매번 덮인다.
        if (bagFill != null) bagFillImage = bagFill.GetComponent<Image>();
        if (bagIcon != null) bagIconImage = bagIcon.GetComponent<Image>();
    }

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
        SetText(dayText, model.Day);
        SetText(phaseText, model.PhaseName);
        SetText(timerText, model.Timer);
        SetText(teamText, model.Team);
        SetText(revenueText, model.Revenue);
        SetText(label, model.Details);

        SetGroup(bagPercentText, model.ShowBag);
        SetGroup(bagWeightText, model.ShowBag);
        // 가방 아이콘은 밤의 획득 연출 목표다 (기획서 6.5.5). 낮에도 켜 두면 정체를 알 수
        // 없는 사각형이 남는다.
        SetGroup(bagIcon, model.ShowBag);
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
                    bagIconImage.color = model.BagBuried
                        ? buriedColor
                        : new Color(0.35f, 0.26f, 0.18f, 0.9f);
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
