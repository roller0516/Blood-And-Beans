using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 매치 HUD의 밤 쪽 — 매출·가방·날짜·시계·귀환 표식. `NightRoot`에 붙고 밤에만 켜진다.
/// 낮과 같이 쓰는 칸(예보·프롬프트·대시·월드 표식)은 `UIMatchHudScreen`이 갖는다.
public sealed class UIMatchHudNight : MonoBehaviour
{
    [Header("가방")]
    /// 루팅한 아이템이 빨려 들어갈 가방 아이콘. 획득 연출의 목적지다.
    [SerializeField] RectTransform bagIcon;
    [SerializeField] UIBagStatus bagStatus;

    /// 가방 아이콘의 두 얼굴. 비어 있거나 꽉 찼으면 닫힌 쪽, 그 사이에는 열린 쪽이다.
    [SerializeField] Sprite bagClosedSprite;
    [SerializeField] Sprite bagOpenSprite;

    /// 가방이 열려 보이는 적재 구간의 위쪽 끝. 이 값을 넘으면 더 들어가지 않으므로 닫는다.
    ///
    /// ponytail: 기획서에 없는 값이다 — 6.5.5는 획득 연출(날아가기·스케일 펀치)까지만
    /// 정하고 아이콘이 열리고 닫히는 규칙은 없다. 100%가 아니라 겉보기가 갈리는
    /// 80%(`LoadBands.OverloadRatio`)에서 닫는 편이 맞다면 인스펙터에서 옮기면 된다.
    [SerializeField, Min(0f)] float bagOpenUntilRatio = 1f;

    [SerializeField] TMP_Text bagPercentText;
    [SerializeField] TMP_Text bagWeightText;
    [SerializeField] RectTransform nightBagDock;
    [SerializeField] RectTransform nightBagSummaryRoot;
    [SerializeField] TMP_Text nightBagSummary;
    [SerializeField] bool showNightBagCapacityDetails;
    [SerializeField] bool compactBagCapacity;
    [SerializeField] string compactCapacityFormat = "{0:0}%";
    [SerializeField] string absentCapacityText = "—";
    [SerializeField] Image playerPortrait;
    [SerializeField] RectTransform bagFill;

    /// 가방을 묻어 두면 게이지와 글자가 이 색으로 바뀐다. 묻힌 동안에는 담기가 전부
    /// 거절되므로 한눈에 보여야 한다 (기획서 6.7 묻기).
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

    [Header("왼쪽 위 — 매출")]
    [SerializeField] TMP_Text revenueText;
    [SerializeField] string nightRevenueFormat = "{0:N0}G";

    [Header("위 가운데 — 날짜·페이즈·시계")]
    [SerializeField] TMP_Text dayText;
    [SerializeField] TMP_Text phaseText;
    [SerializeField] TMP_Text timerText;
    [SerializeField] bool compactNightClock = true;

    [Header("귀환 표시")]
    [SerializeField] RectTransform returnBox;
    [SerializeField] RectTransform returnArrow;
    [SerializeField] TMP_Text returnLabel;

    // 밤 마감 30초 전에 울리는 종소리 (기획서 6.4). 애셋이 아직 없어서 비워 둘 수 있고,
    // 비어 있으면 화살표만 뜬다 — 소리는 이 표시의 필수 조건이 아니다.
    [SerializeField] AudioClip returnAlarmSound;

    /// 마커가 화면 밖으로 나갔을 때 가장자리에서 띄워 두는 거리. 0이면 마커의 절반이
    /// 화면 밖으로 잘려 나간다.
    [SerializeField] Vector2 markerEdgeMargin = new(96f, 72f);

    /// 매 갱신마다 찾지 않으려고 캐시한다. HUD 갱신은 0.1초마다 도는 주기 실행이다 (CLAUDE.md).
    Image bagFillImage;
    Image bagIconImage;

    /// 이번 경보의 종을 이미 울렸는가. 경보가 꺼지면 풀려서 다음 밤에 다시 울린다.
    bool alarmRung;

    /// 가방 연출의 목적지. 절대 null이 아니다.
    public RectTransform BagAnchor => bagIcon;

    void Awake()
    {
        if (bagFill != null) bagFillImage = bagFill.GetComponent<Image>();
        if (bagIcon != null) bagIconImage = bagIcon.GetComponent<Image>();
    }

    /// 무게 구간에 맞는 색. 표가 비어 있어도 게이지가 사라지면 안 되므로 원색으로 떨어진다.
    Color BandColor(int band)
    {
        if (bagBandColors == null || bagBandColors.Length == 0) return Color.white;
        return bagBandColors[Mathf.Clamp(band, 0, bagBandColors.Length - 1)];
    }

    public void Render(in MatchHudModel model)
    {
        if (playerPortrait != null)
        {
            playerPortrait.sprite = model.Portrait;
            playerPortrait.enabled = model.Portrait != null;
        }
        UIMatchHudScreen.SetText(dayText, model.Day);
        UIMatchHudScreen.SetText(phaseText, model.PhaseName);
        UIMatchHudScreen.SetText(timerText, compactNightClock && model.Timer != null ? model.Timer.Split('.')[0] : model.Timer);
        UIMatchHudScreen.SetText(revenueText, model.TeamRevenue.HasValue ? string.Format(nightRevenueFormat, model.TeamRevenue.Value) : model.Revenue);

        UIMatchHudScreen.SetGroup(nightBagDock, model.ShowBag);
        UIMatchHudScreen.SetGroup(nightBagSummaryRoot, model.ShowBag);
        var capacity = compactBagCapacity
            ? model.BagBuried ? absentCapacityText : string.Format(compactCapacityFormat, model.BagRatio * 100f)
            : model.BagPercent;
        UIMatchHudScreen.SetText(nightBagSummary, compactBagCapacity ? capacity : model.BagWeight);
        UIMatchHudScreen.SetGroup(bagPercentText, model.ShowBag && showNightBagCapacityDetails);
        UIMatchHudScreen.SetGroup(bagWeightText, model.ShowBag);
        UIMatchHudScreen.SetGroup(bagIcon, model.ShowBag);
        if (bagStatus != null) bagStatus.SetMissing(model.ShowBag && model.BagBuried);
        if (bagFill == null) return;

        UIMatchHudScreen.SetGroup(bagFill, model.ShowBag && showNightBagCapacityDetails);
        if (!model.ShowBag) return;

        UIMatchHudScreen.SetText(bagPercentText, capacity);
        UIMatchHudScreen.SetText(bagWeightText, compactBagCapacity && model.BagBuried ? model.BagPercent : model.BagWeight);
        bagFill.localScale = new Vector3(Mathf.Clamp01(model.BagRatio), 1f, 1f);

        // 묻어 둔 동안에는 적재량이 의미가 없다. 게이지를 비우고 색으로 알린다.
        // 메고 있으면 무게 구간이 색을 정한다 (기획서 6.7).
        var tint = model.BagBuried ? buriedColor : BandColor(model.BagBand);
        if (bagFillImage != null) bagFillImage.color = tint;
        if (bagPercentText != null) bagPercentText.color = tint;
        if (bagIconImage == null) return;

        // 아이콘은 그림 색 그대로 둔다. 색을 곱하면 열린 가방과 닫힌 가방이 둘 다 그 색으로
        // 뭉개진다. 묻어 둔 것은 흐리게 해서만 알린다 — X 표시는 `UIBagStatus`가 그린다.
        bagIconImage.color = new Color(1f, 1f, 1f, model.BagBuried ? bagBuriedAlpha : 1f);

        // 하나라도 담겨 있으면 열어 둔다. 묻어 둔 가방은 적재가 0으로 와서 저절로 닫힌다.
        var open = model.BagRatio > 0f && model.BagRatio < bagOpenUntilRatio;
        var face = open ? bagOpenSprite : bagClosedSprite;
        if (face != null && bagIconImage.sprite != face) bagIconImage.sprite = face;
    }

    /// 매 프레임 불린다. 마커는 월드의 한 점에 붙어 있어서 HUD 갱신 주기(0.1초)로 옮기면
    /// 카메라가 도는 동안 계단처럼 끊긴다.
    ///
    /// 화면 안이면 카페 위에 그대로 뜨고 화살표는 감춘다. 눈에 보이는 것을 두고 방향까지
    /// 가리킬 이유가 없다. 화면 밖이면 가장자리에 붙고 화살표가 그쪽을 가리킨다.
    public void SetReturnMarker(in MatchHudPresenter.ReturnMarker marker)
    {
        if (returnBox == null) return;

        UIMatchHudScreen.SetGroup(returnBox, marker.Show);
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
        UIMatchHudScreen.SetGroup(returnArrow, marker.Offscreen);
        UIMatchHudScreen.SetText(returnLabel, marker.Label);
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

    /// 경보 구간에 들어간 순간 한 번만 울린다. 듣는 사람 귀에 붙는 소리라 2D로 SFX 버스에 낸다.
    void RingOnce()
    {
        if (alarmRung) return;
        alarmRung = true;

        SoundManager.Instance.PlaySfx(returnAlarmSound);
    }
}
