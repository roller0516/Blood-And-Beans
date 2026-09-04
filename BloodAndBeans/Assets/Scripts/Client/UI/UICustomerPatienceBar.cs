using UnityEngine;
using UnityEngine.UI;

/// 손님 머리 위의 인내심 게이지 (기획서 5.5).
///
/// 다 닳으면 나가고 매출이 0이라, 남은 시간은 어느 주문을 먼저 칠지 정하는 정보다.
/// 매장 반대편에서 한눈에 읽혀야 해서 숫자가 아니라 막대다.
///
/// 캔버스와 막대는 런타임에 만든다. 손님 프리팹에 이 컴포넌트만 붙이면 된다.
[RequireComponent(typeof(Customer))]
public class UICustomerPatienceBar : MonoBehaviour
{
    [Header("배치")]
    /// 발밑에서 막대까지의 높이. 종족마다 키가 달라도 머리 위에 오도록 넉넉히 둔다.
    [SerializeField] float height = 2.1f;

    /// 막대 크기(월드 단위).
    [SerializeField] Vector2 size = new(0.9f, 0.12f);

    [Header("색")]
    [SerializeField] Color full = new(0.45f, 0.80f, 0.40f);
    [SerializeField] Color low = new(0.90f, 0.30f, 0.25f);
    [SerializeField] Color back = new(0f, 0f, 0f, 0.55f);

    /// 「붙임성」이 걸려 닳지 않는 손님 (기획서 9.1). 다른 색으로 구분한다.
    [SerializeField] Color patient = new(0.55f, 0.75f, 0.95f);

    /// 이 비율 아래부터 경고색으로 넘어간다.
    [SerializeField, Range(0f, 1f)] float lowAt = 0.35f;

    /// 월드 캔버스는 픽셀로 잡고 스케일로 줄인다. 월드 단위(0.9 등)를 sizeDelta에 그대로
    /// 넣으면 UI가 서브픽셀이 되어 아무것도 그려지지 않는다.
    const float PixelsPerUnit = 100f;

    Customer customer;
    RectTransform bar;
    Image fill;
    Camera view;

    void Awake()
    {
        customer = GetComponent<Customer>();
        Build();
    }

    void Build()
    {
        var root = new GameObject("PatienceBar", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        root.transform.localPosition = Vector3.up * height;

        root.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;

        bar = (RectTransform)root.transform;
        bar.sizeDelta = size * PixelsPerUnit;
        bar.localScale = Vector3.one / PixelsPerUnit;

        Stretch(NewImage(bar, "Back", back).rectTransform);

        // 채움은 왼쪽 피벗을 잡고 x 스케일로 줄인다. `Image.Type.Filled`는 스프라이트가
        // 있어야 하는데 런타임 생성이라 없다 — HUD 가방 게이지와 같은 방식이다.
        fill = NewImage(bar, "Fill", full);
        var rect = fill.rectTransform;

        // 왼쪽 변에 붙여 세로만 늘린다. 폭은 sizeDelta.x가 갖고, 줄어드는 것은 스케일이다.
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.sizeDelta = new Vector2(size.x * PixelsPerUnit, 0f);
        rect.anchoredPosition = Vector2.zero;
    }

    static Image NewImage(Transform parent, string name, Color colour)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);

        var image = go.GetComponent<Image>();
        image.color = colour;
        image.raycastTarget = false;
        return image;
    }

    static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    /// 값 갱신과 빌보드를 함께 한다. 인내심은 매 프레임 줄고(`Customer.Update`) 카메라도
    /// 매 프레임 돈다.
    void LateUpdate()
    {
        if (fill == null || customer == null) return;

        var ratio = Mathf.Clamp01(customer.PatienceRatio);
        fill.rectTransform.localScale = new Vector3(ratio, 1f, 1f);
        fill.color = customer.Patient ? patient : Color.Lerp(low, full, Mathf.InverseLerp(0f, lowAt, ratio));

        // 카메라는 밤낮으로 갈리므로(`MatchCameraDirector`) 캐시하되 사라지면 다시 찾는다.
        if (view == null) view = Camera.main;
        if (view != null) bar.forward = view.transform.forward;
    }
}
