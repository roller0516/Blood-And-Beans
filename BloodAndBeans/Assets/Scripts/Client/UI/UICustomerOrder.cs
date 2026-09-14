using UnityEngine;
using Unity.Netcode;

/// 카페 안에서는 모든 손님의 주문을 펼친다 (기획서 5.7.2).
public sealed class UICustomerOrder : MonoBehaviour
{
    [SerializeField] Canvas canvas;
    [SerializeField] UIRing patience;
    [SerializeField] UIDayItemIcon iconPrefab;
    [SerializeField] Transform icons;
    [SerializeField] UIThemeConfig theme;
    [SerializeField] AudioSource warning;
    [SerializeField] TMPro.TMP_Text expectedPrice;
    [SerializeField] Color calm = new(0.5f, 0.85f, 0.35f);
    [SerializeField] Color urgent = new(1f, 0.25f, 0.2f);
    [SerializeField] float shakeDegrees = 3f;
    Customer customer;
    Cafe cafe;
    Camera cameraView;
    Transform player;
    Collider floor;
    TransitionLedger ledger;
    UIDayItemIcon[] slots;
    float refreshAt;
    bool wasUrgent;
    void Awake()
    {
        customer = GetComponentInParent<Customer>();
        slots = new UIDayItemIcon[6];
        for (var i = 0; i < slots.Length; i++) slots[i] = Instantiate(iconPrefab, icons);
        canvas.enabled = false;
    }
    void LateUpdate()
    {
        if (customer == null || !customer.IsSpawned) { canvas.enabled = false; return; }
        if (cameraView == null) cameraView = Camera.main;
        if (player == null) player = NetworkManager.Singleton?.LocalClient?.PlayerObject?.transform;
        if (cafe == null) cafe = MatchDirector.Instance?.CafeOf(customer.TeamId);
        if (floor == null && cafe != null) floor = cafe.Floor;
        if (ledger == null && MatchDirector.Instance != null) ledger = MatchDirector.Instance.GetComponent<TransitionLedger>();
        var ownDay = cafe != null && cafe.TeamId == PlayerTeam.Local() && cafe.Director != null &&
            cafe.Director.Phase.Current == Phase.Day;
        var visible = ownDay && player != null && floor != null && InsideFloor(floor.bounds, player.position);
        canvas.enabled = visible;
        var ratio = Mathf.Clamp01(customer.PatienceRatio);
        var isUrgent = ownDay && ratio <= DayBalance.PatienceUrgent;
        if (isUrgent && !wasUrgent && warning != null && warning.clip != null) warning.Play();
        wasUrgent = isUrgent;
        if (!visible) return;
        patience.Amount = ratio;
        patience.color = isUrgent ? urgent : ratio <= DayBalance.PatienceWarning ? theme.Gold : calm;
        if (cameraView != null) transform.rotation = cameraView.transform.rotation *
            Quaternion.Euler(0f, 0f, isUrgent ? Mathf.Sin(Time.time * 12f) * shakeDegrees : 0f);
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 0.15f;
        RenderIcons();
    }
    public static bool InsideFloor(Bounds bounds, Vector3 point) =>
        point.x >= bounds.min.x && point.x <= bounds.max.x && point.z >= bounds.min.z && point.z <= bounds.max.z;
    void RenderIcons()
    {
        var used = 0;
        if (expectedPrice != null) expectedPrice.text = string.Empty;
        foreach (var menu in Menus.All)
        {
            if (!customer.Accepts(Menus.TagsOf(menu.Parts), menu.Parts.Length)) continue;
            var product = menu.Parts[0];
            var copies = customer.Kind == Race.Werewolf ? customer.Remaining : 1;
            for (var i = 0; i < copies && used < slots.Length; i++) Draw(used++, product, true);
            for (var i = 1; i < menu.Parts.Length && used < slots.Length; i++) Draw(used++, menu.Parts[i], false);
            if (expectedPrice != null)
                expectedPrice.text = ExpectedPrice(menu, customer.Kind, ledger?.PopularShown).ToString();
            break;
        }
        for (var i = 0; i < slots.Length; i++) slots[i].gameObject.SetActive(i < used);
    }
    // 기획서 5.7.2: 아직 정하지 않은 완성 판정은 제외하고 기존 판매가 원본을 재사용한다.
    public static int ExpectedPrice(MenuDef menu, Race race, Ingredient[] popular) =>
        Mathf.RoundToInt(SalePrice.Calculate(menu.BasePrice, Gauge.Good, BeanGrade.Normal,
            System.Array.IndexOf(menu.Parts, Ingredient.BreadBase) >= 0, menu.Parts, popular) * Customer.PriceWeightOf(race));
    void Draw(int index, Ingredient item, bool product)
    {
        var available = product || Ingredients.IsStaple(item) || cafe.Stock.CountOf(item) > 0;
        var popular = ledger != null && System.Array.IndexOf(ledger.PopularShown, item) >= 0;
        slots[index].Render(theme.DaySprite(item), null, available, popular, item == Ingredient.BloodBean);
    }
}
