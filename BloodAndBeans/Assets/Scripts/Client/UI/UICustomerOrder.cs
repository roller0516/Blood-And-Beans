using UnityEngine;
using Unity.Netcode;

/// 카페 안에서는 모든 손님의 주문을 펼친다 (기획서 5.7.2).
/// 이 컴포넌트와 경고음은 손님 머리 위에 남고, 그림(`view`)은 HUD 표식 층에서 화면 크기로 그린다.
public sealed class UICustomerOrder : MonoBehaviour
{
    [SerializeField] RectTransform view;
    [SerializeField] CanvasGroup group;
    /// Filled·Radial 360 Image. 시작 위치와 방향은 프리팹에서 정한다.
    [SerializeField] UnityEngine.UI.Image patience;
    /// 완성품만 보인다. 조합식은 HUD의 F1 패널에서 본다 (`UIMatchHudScreen.ToggleRecipe`).
    [SerializeField] UINamedItemIcon product;
    [SerializeField] UIThemeConfig theme;
    [SerializeField] AudioSource warning;
    [SerializeField] TMPro.TMP_Text expectedPrice;
    [SerializeField] Color calm = new(0.5f, 0.85f, 0.35f);
    [SerializeField] Color urgent = new(1f, 0.25f, 0.2f);
    [SerializeField] float shakeDegrees = 3f;    Customer customer;
    Cafe cafe;
    Camera cameraView;
    UIMatchHudScreen hud;
    Transform player;
    Collider floor;
    TransitionLedger ledger;
    float refreshAt;
    bool wasUrgent;
    void Awake()
    {
        customer = GetComponentInParent<Customer>();
        group.alpha = 0f;
    }
    // 그림은 HUD 층으로 옮겨 가 있어 손님과 함께 파괴되지 않는다.
    void OnDestroy()
    {
        if (view != null) Destroy(view.gameObject);
    }
    void LateUpdate()
    {
        // 플레이·앱 종료 때는 HUD가 이 컴포넌트보다 먼저 파괴되며 그 층에 옮겨 둔 그림을 데려간다.
        if (view == null) return;
        if (customer == null || !customer.IsSpawned) { group.alpha = 0f; return; }
        if (cameraView == null) cameraView = Camera.main;
        if (hud == null) UIManager.Instance.TryGet(out hud);
        if (player == null) player = NetworkManager.Singleton?.LocalClient?.PlayerObject?.transform;
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
        if (!Mathf.Approximately(patience.fillAmount, ratio)) patience.fillAmount = ratio;
        patience.color = isUrgent ? urgent : ratio <= DayBalance.PatienceWarning ? theme.Gold : calm;
        view.localRotation = Quaternion.Euler(0f, 0f, isUrgent ? Mathf.Sin(Time.time * 12f) * shakeDegrees : 0f);
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 0.15f;
        RenderProduct();
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
            if (expectedPrice != null)
                expectedPrice.text = ExpectedPrice(menu, customer.Kind, ledger?.PopularShown).ToString();
            break;
        }
    }
    // 기획서 5.7.2: 아직 정하지 않은 완성 판정은 제외하고 기존 판매가 원본을 재사용한다.
    public static int ExpectedPrice(MenuDef menu, Race race, Ingredient[] popular) =>
        Mathf.RoundToInt(SalePrice.Calculate(menu.BasePrice, Gauge.Good, BeanGrade.Normal,
            System.Array.IndexOf(menu.Parts, Ingredient.BreadBase) >= 0, menu.Parts, popular) * Customer.PriceWeightOf(race));
    bool CanMake(MenuDef menu)
    {
        foreach (var item in menu.Parts)
            if (!Ingredients.IsStaple(item) && cafe.Stock.CountOf(item) <= 0) return false;
        return true;
    }
}
