using UnityEngine;
using Unity.Netcode;

/// 카페 안에서는 모든 손님의 주문을 펼친다 (기획서 5.7.2).
/// 이 컴포넌트와 경고음은 손님 머리 위에 남고, 그림(`view`)은 HUD 표식 층에서 화면 크기로 그린다.
public sealed class UICustomerOrder : MonoBehaviour
{
    [SerializeField] RectTransform view;
    [SerializeField] CanvasGroup group;
    /// 말풍선 배경. 써 버린 인내심만큼 아래에서부터 차오른다 (기획서 5.7.2).
    [SerializeField] UIBubbleFill patience;
    /// 완성품만 보인다. 조합식은 HUD의 F1 패널에서 본다 (`UIMatchHudScreen.ToggleRecipe`).
    [SerializeField] UINamedItemIcon product;
    [SerializeField] UIThemeConfig theme;
    [SerializeField] AudioSource warning;
    [SerializeField] TMPro.TMP_Text expectedPrice;
    /// 받을 돈을 얹는 코인 탭 바탕. Miss·탄 것이면 붉어진다 (기획서 5.7.2).
    [SerializeField] UnityEngine.UI.Graphic coinTab;
    /// 판정 화살표. 위를 향한 그림 하나를 Miss·탄 것에서 뒤집어 쓴다.
    [SerializeField] UnityEngine.UI.Image judgementArrow;
    [SerializeField] Color calm = new(0.5f, 0.85f, 0.35f);
    [SerializeField] Color urgent = new(1f, 0.25f, 0.2f);
    [SerializeField] float shakeDegrees = 3f;    Customer customer;
    Cafe cafe;
    Camera cameraView;
    UIMatchHudScreen hud;
    Transform player;
    PlayerCarry carry;
    Color priceColor, tabColor;
    Collider floor;
    TransitionLedger ledger;
    float refreshAt;
    bool wasUrgent;
    bool saleClosed;
    void Awake()
    {
        customer = GetComponentInParent<Customer>();
        group.alpha = 0f;
        if (expectedPrice != null) priceColor = expectedPrice.color;
        if (coinTab != null) tabColor = coinTab.color;
    }
    // 그림은 HUD 층으로 옮겨 가 있어 손님과 함께 파괴되지 않는다.
    void OnDestroy()
    {
        if (view != null) Destroy(view.gameObject);
    }
    void LateUpdate()
    {
        // 플레이·앱 종료 때는 HUD가 이 컴포넌트보다 먼저 파괴되며 그 층에 옮겨 둔 그림을 데려간다.
        if (view == null || saleClosed) return;
        if (customer == null || !customer.IsSpawned) { group.alpha = 0f; return; }
        if (cameraView == null) cameraView = Camera.main;
        if (hud == null) UIManager.Instance.TryGet(out hud);
        if (player == null) player = NetworkManager.Singleton?.LocalClient?.PlayerObject?.transform;
        if (carry == null && player != null) carry = player.GetComponent<PlayerCarry>();
        if (cafe == null) cafe = MatchDirector.Instance?.CafeOf(customer.TeamId);
        if (floor == null && cafe != null) floor = cafe.Floor;
        if (ledger == null && MatchDirector.Instance != null) ledger = MatchDirector.Instance.GetComponent<TransitionLedger>();
        var ownDay = cafe != null && cafe.TeamId == PlayerTeam.Local() && cafe.Director != null &&
            cafe.Director.Phase.Current == Phase.Day;
        var visible = ownDay && player != null && floor != null && InsideFloor(floor.bounds, player.position)
            && cameraView != null && hud != null && hud.PlaceMarker(view, transform.position, cameraView);
        group.alpha = visible ? 1f : 0f;
        var ratio = Mathf.Clamp01(customer.PatienceRatio);
        var isUrgent = ownDay && ratio <= DayBalance.PatienceUrgent;
        if (isUrgent && !wasUrgent && warning != null && warning.clip != null) warning.Play();
        wasUrgent = isUrgent;
        if (!visible) return;
        patience.Show(1f - ratio, isUrgent ? urgent : ratio <= DayBalance.PatienceWarning ? theme.Gold : calm);
        view.localRotation = Quaternion.Euler(0f, 0f, isUrgent ? Mathf.Sin(Time.time * 12f) * shakeDegrees : 0f);
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 0.15f;
        RenderProduct();
    }
    /// 판매 파열은 HUD가 맡고 원래 말풍선만 감춘다.
    public bool TrySaleAnchor(bool close, out Vector3 point, out Vector2 pixels)
    {
        point = Vector3.zero; pixels = Vector2.zero;
        if (view == null || group.alpha <= 0f || !view.gameObject.activeInHierarchy) return false;
        point = view.TransformPoint(view.rect.center);
        pixels = Vector2.Scale(view.rect.size, new Vector2(view.lossyScale.x, view.lossyScale.y));
        if (close) { saleClosed = true; group.alpha = 0f; }
        return true;
    }
    public static bool InsideFloor(Bounds bounds, Vector3 point) =>
        point.x >= bounds.min.x && point.x <= bounds.max.x && point.z >= bounds.min.z && point.z <= bounds.max.z;
    void RenderProduct()
    {
        if (expectedPrice != null) expectedPrice.text = string.Empty;
        foreach (var menu in Menus.All)
        {
            if (!customer.Accepts(Menus.TagsOf(menu.Parts), menu.Parts.Length)) continue;
            // 늑대인간은 같은 것을 여러 잔 주문한다 (기획서 5.7.2).
            var copies = customer.Kind == Race.Werewolf ? customer.Remaining : 1;
            // 우리 재고로 못 만드는 주문은 흐리게 둔다. 재료 아이콘을 흐리던 규칙을 완성품에 옮긴 것이다.
            product.Show(ResourceManager.Instance.MenuSprite(menu.Id), string.Empty, copies > 1 ? $"×{copies}" : null, CanMake(menu), false, SalePrice.PopularCount(menu.Parts, ledger?.PopularShown) > 0);
            ShowPrice(menu, copies, ledger?.PopularShown);
            break;
        }
    }
    /// 코인 탭은 잔 수와 무관하게 합계 하나다. 이 주문에 맞는 완성품을 든 동안만 그 잔에 판정까지 곱한다 (기획서 5.7.2).
    void ShowPrice(MenuDef order, int copies, Ingredient[] popular)
    {
        if (expectedPrice == null) return;
        var basePrice = ExpectedPrice(order, customer.Kind, popular);
        var total = basePrice * copies;
        var gauge = Gauge.Good;
        var held = carry != null ? carry.View : CarryView.Nothing;
        if (held.IsProduct && TryMenu(held.Menu, out var heldMenu) &&
            customer.Accepts(Menus.TagsOf(heldMenu.Parts), heldMenu.Parts.Length))
        {
            gauge = held.Gauge;
            var grade = carry.BloodGlow ? BeanGrade.Blood : BeanGrade.Normal;
            total += ExpectedPrice(heldMenu, customer.Kind, popular, gauge, grade) - basePrice;
        }
        // Good은 기준이라 아무것도 붙이지 않는다. 숫자는 적용된 값 하나만 쓴다.
        var up = gauge == Gauge.Perfect;
        var down = gauge == Gauge.Miss || gauge == Gauge.Burnt;
        expectedPrice.text = total.ToString();
        expectedPrice.color = up ? theme.GoldLit : down ? theme.Cream : priceColor;
        if (coinTab != null) coinTab.color = down ? theme.Red : tabColor;
        if (judgementArrow == null) return;
        judgementArrow.gameObject.SetActive(up || down);
        judgementArrow.color = expectedPrice.color;
        judgementArrow.rectTransform.localRotation = down ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;
    }
    static bool TryMenu(MenuId id, out MenuDef menu)
    {
        foreach (var m in Menus.All)
            if (m.Id == id) { menu = m; return true; }
        menu = default;
        return false;
    }
    // 기획서 5.7.2: 기본값은 완성 판정을 뺀 값이다. 기존 판매가 원본을 재사용한다.
    public static int ExpectedPrice(MenuDef menu, Race race, Ingredient[] popular) =>
        ExpectedPrice(menu, race, popular, Gauge.Good, BeanGrade.Normal);
    public static int ExpectedPrice(MenuDef menu, Race race, Ingredient[] popular, Gauge gauge, BeanGrade grade) =>
        Mathf.RoundToInt(SalePrice.Calculate(menu.BasePrice, gauge, grade,
            System.Array.IndexOf(menu.Parts, Ingredient.BreadBase) >= 0, menu.Parts, popular) * Customer.PriceWeightOf(race));
    bool CanMake(MenuDef menu)
    {
        foreach (var item in menu.Parts)
            if (!Ingredients.IsStaple(item) && cafe.Stock.CountOf(item) <= 0) return false;
        return true;
    }
}
