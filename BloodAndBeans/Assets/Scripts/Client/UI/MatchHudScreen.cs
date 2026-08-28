using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 매치 중 상시 떠 있는 HUD. 날짜·페이즈·남은 시간·팀·카페 상태를 보여 준다.
///
/// 화면 오른쪽에 붙는다.
///
/// 이 클래스는 값을 만들지 않는다. 무엇을 쓸지는 `MatchHudPresenter`가 정한다 —
/// 예전에는 한 클래스가 캔버스를 만들고, 복제 상태를 읽고, 문자열을 조립하고, 로컬
/// 플레이어 컴포넌트까지 캐시했다.
public sealed class MatchHudScreen : UIScreen
{
    [SerializeField] TMP_Text label;

    /// 루팅한 아이템이 빨려 들어갈 가방 아이콘. 프리팹에 이어 두지 않으면 아래에서
    /// 만들어 쓴다 — 이 아이콘은 연출의 목적지라서 없으면 획득 피드백이 사라진다.
    [SerializeField] RectTransform bagIcon;

    /// 프리팹에 아이콘이 없을 때 만들 자리와 크기. 화면 오른쪽 아래 모서리 기준이다.
    [SerializeField] Vector2 bagIconSize = new(72f, 72f);
    [SerializeField] Vector2 bagIconMargin = new(-72f, 72f);

    [Header("개봉 게이지")]
    /// 상자 개봉 로딩 바. 화면 한가운데에 뜬다 — 이건 지금 무엇을 기다리는지 알려 주는
    /// 값이라 HUD 오른쪽 글자 덩어리에 섞으면 시선이 닿지 않는다.
    [SerializeField] Vector2 castBarSize = new(260f, 14f);

    /// 화면 중앙에서 내리는 양. 정확히 가운데는 캐릭터와 겹친다.
    [SerializeField] float castBarDrop = 90f;

    [SerializeField] Color castBarBack = new(0f, 0f, 0f, 0.55f);
    [SerializeField] Color castBarFill = new(1f, 0.82f, 0.29f);

    RectTransform castBar;
    RectTransform castFill;

    /// 가방 연출의 목적지. 절대 null이 아니다.
    public RectTransform BagAnchor => bagIcon;

    protected override void Awake()
    {
        base.Awake();
        if (bagIcon == null) bagIcon = BuildBagIcon();
        BuildCastBar();
    }

    public void Render(string value)
    {
        if (label != null) label.text = value;
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
