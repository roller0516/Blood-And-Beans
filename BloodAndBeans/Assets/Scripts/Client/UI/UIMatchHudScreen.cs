using TMPro;
using UnityEngine;

/// 매치 중 상시 떠 있는 HUD. 낮과 밤이 같이 쓰는 칸(예보·프롬프트·대시·개봉 게이지·월드 표식)을
/// 갖고, 페이즈별 칸은 `UIMatchHudDay`·`UIMatchHudNight`에 맡긴다. 페이즈마다 한쪽 루트만 켠다.
///
/// 이 클래스는 값을 만들지 않는다. 무엇을 쓸지는 `MatchHudPresenter`가 정하고, 여기는
/// 받은 값을 정해진 자리에 그리기만 한다.
///
/// **트리는 프리팹에 있다.** 이 클래스는 아무것도 만들지 않고 이어 둔 참조에 값만 넣는다.
/// 예전에는 코드로 세웠지만, 그러면 자리·색·글꼴을 기획자가 손댈 수 없었다.
public sealed class UIMatchHudScreen : UIScreen
{
    [Header("페이즈별 루트")]
    [SerializeField] UIMatchHudDay day;
    [SerializeField] UIMatchHudNight night;

    /// 페이즈별 상세(예보·순위·접시·손님). 자리를 잡아 줄 수 없는 가변 길이라 오른쪽에
    /// 한 덩어리로 흘린다.
    [SerializeField] TMP_Text label;

    /// 월드 좌표를 따라가는 표식(주문 말풍선·재료 배지)이 올라가는 층. HUD 맨 아래에 깐다.
    /// ponytail: 표식끼리 앞뒤 정렬은 하지 않는다. 겹쳐 보이는 일이 생기면 카메라 거리로 정렬한다.
    [SerializeField] RectTransform worldMarkers;

    /// 표식 크기는 기준 깊이에서 1배, 멀수록 반비례로 줄고 범위 안에 묶인다.
    /// ponytail: 기획서에 없는 연출값이다. 플레이해 보며 인스펙터에서 맞춘다.
    [SerializeField, Min(0.01f)] float markerReferenceDepth = 5f;
    [SerializeField] Vector2 markerScaleRange = new(0.35f, 1f);

    [Header("색")]
    [SerializeField] Color muted = new(0.72f, 0.72f, 0.68f, 0.9f);  // 라벨
    [SerializeField] Color panelBack = new(0.04f, 0.05f, 0.05f, 0.55f);

    [Header("여백")]
    [SerializeField] float margin = 24f;

    [Header("오른쪽 위")]
    [SerializeField] TMP_Text teamText;

    /// 낮·밤 헤더와 따로 둔다. 헤더는 페이즈마다 꺼지지만 핑은 늘 보여야 한다.
    [SerializeField] TMP_Text pingText;

    [Header("상호작용 안내")]
    [SerializeField] RectTransform promptBox;
    [SerializeField] TMP_Text promptText;

    /// 건네기 프롬프트가 발밑에서 뜨는 높이(m). 이름표(1.5m)보다 위다.
    [SerializeField, Min(0f)] float handoffHeight = 2f;

    /// 건네기 프롬프트 두 개 — 나와 팀원의 머리 위 (기획서 5.7.4). `promptBox`를 처음 쓸 때 복제한다.
    RectTransform[] handoffBoxes;
    TMP_Text[] handoffTexts;

    [Header("개봉 게이지")]
    [SerializeField] RectTransform castBar;
    [SerializeField] RectTransform castFill;

    [Header("대시")]
    [SerializeField] UISkillSlot dashSlot;

    public UIMatchHudDay Day => day;
    public UIMatchHudNight Night => night;
    public RectTransform WorldMarkers => worldMarkers;

    /// HUD는 누르는 곳이 없다. 밤에는 마우스가 카메라를 돌리므로 커서를 잠근다.
    public override bool WantsCursor => day.RecipeOpen;

    /// 표식은 진열대·손님이 주인이다(주인이 자기 OnDestroy에서 지운다). HUD가 내려갈 때 같이 파괴되지
    /// 않게 층 밖으로 내보낸다. HUD가 다시 뜨면 `PlaceMarker`가 다시 데려온다.
    public override void OnUnload()
    {
        // 건네기 표식만은 HUD가 주인이다. 층 밖으로 내보내면 주인 없이 씬에 남는다.
        if (handoffBoxes != null)
            foreach (var box in handoffBoxes) Destroy(box.gameObject);
        handoffBoxes = null;
        handoffTexts = null;
        day.Unload();

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

    /// 한 번에 한 덩어리로 받는다. 칸마다 따로 부르면 어느 칸이 이번 갱신에 빠졌는지
    /// 부르는 쪽이 기억해야 한다.
    public void Render(in MatchHudModel model)
    {
        ShowPhase(model.IsDay);
        if (model.IsDay) day.Render(model);
        else night.Render(model);

        SetText(teamText, model.Team);
        SetText(pingText, model.Ping);
        SetText(label, model.Details);

        var hasPrompt = !string.IsNullOrEmpty(model.Prompt);
        SetGroup(promptBox, hasPrompt);
        if (hasPrompt) SetText(promptText, model.Prompt);

        SetGroup(dashSlot, model.ShowDash);
        if (model.ShowDash)
        {
            dashSlot.SetCooldown(model.DashRatio);
            dashSlot.SetCount(model.DashTime);
            dashSlot.SetKey(model.DashKey);
        }
        else dashSlot.ResetCooldown();
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

    /// 매 프레임 불린다. 두 사람이 움직이는 동안 머리를 따라가야 한다.
    public void SetHandoff(in MatchHudPresenter.HandoffMarker marker)
    {
        if (!marker.Show)
        {
            if (handoffBoxes != null)
                foreach (var box in handoffBoxes) SetGroup(box, false);
            return;
        }

        if (handoffBoxes == null)
        {
            handoffBoxes = new RectTransform[2];
            handoffTexts = new TMP_Text[2];
            for (var i = 0; i < handoffBoxes.Length; i++)
            {
                handoffBoxes[i] = Instantiate(promptBox, worldMarkers);
                handoffTexts[i] = handoffBoxes[i].GetComponentInChildren<TMP_Text>(true);
            }
        }

        PlaceHandoff(0, marker.Self, marker);
        PlaceHandoff(1, marker.Mate, marker);
    }

    void PlaceHandoff(int index, Vector3 feet, in MatchHudPresenter.HandoffMarker marker)
    {
        var box = handoffBoxes[index];
        var visible = PlaceMarker(box, feet + Vector3.up * handoffHeight, marker.View);
        SetGroup(box, visible);
        if (visible && handoffTexts[index].text != marker.Label) SetText(handoffTexts[index], marker.Label);
    }

    internal static void SetText(TMP_Text target, string value)
    {
        if (target != null) target.text = value ?? string.Empty;
    }

    internal static void SetGroup(Component target, bool active)
    {
        if (target != null && target.gameObject.activeSelf != active) target.gameObject.SetActive(active);
    }

    /// 낮/밤 루트를 한쪽만 켜는 유일한 길. 낮 루트를 끄기 전에 닫는다 — 꺼진 뒤에는 펼친
    /// 조합식이 안 보이는데 커서 잠금만 남는다.
    public void ShowPhase(bool isDay)
    {
        if (!isDay) day.Close();
        SetGroup(day, isDay);
        SetGroup(night, !isDay);
    }
}

/// HUD 한 갱신분의 값. 화면이 그리기만 하도록 자리별로 나눠 담는다 — 한 덩어리 문자열로
/// 넘기면 어느 칸에 무엇이 들어갈지를 화면이 다시 파싱해야 한다.
public struct MatchHudModel
{
    public bool IsDay;
    public bool RentMet;
    public float DayRemaining;
    public float DayTimeRatio;
    public string DayCounter;
    public int DaySales;
    public int DayBill;
    public System.Collections.Generic.IReadOnlyList<Standing> Standings;

    /// 공개 정보판에서 받은 누적 매출. 행의 순서는 프레젠터가 정한다 (기획서 3.1·3.4).
    public struct Standing
    {
        public int Team;
        public string Name;
        public int Revenue;
        public bool Mine;
    }
    public string Day;          // "2일차"
    public string PhaseName;    // "야간 탐색"
    public string Timer;        // "02:46"
    public string Team;         // "Team 0"
    public string Ping;         // "핑 42ms" / "호스트"
    public string Revenue;      // "팀 매출  2,840G"
    public int? TeamRevenue;    // 숫자와 고정 라벨을 따로 배치하는 밤 매출판.
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
    public string DashTime;     // 원 안의 남은 초 "6" / "과적". 준비면 빈 문자열
    public float DashRatio;     // 남은 쿨다운 비율. 0이면 준비
    public string DashKey;      // 원 아래의 키
}
