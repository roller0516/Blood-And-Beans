using UnityEngine;

/// 재료대는 재고가 없어도 남기고 배지만 흐린다 (5.7.3).
/// 이 컴포넌트는 진열대에 붙어 자리를 정하고, 그림(`view`)은 HUD 표식 층에서 화면 크기로 그린다.
public sealed class UIIngredientBadge : MonoBehaviour
{
    [SerializeField] RectTransform view;
    [SerializeField] CanvasGroup group;
    [SerializeField] UIDayItemIcon icon;
    /// 카페 재료대는 카페 안에 있으면 늘 띄우고, 광장 설비는 로컬 플레이어가 이 거리 안에 있을 때만 띄운다.
    // ponytail: 기획서 5.7.3에 표시 거리가 없다. 레벨 치수(14장 #4)가 정해지면 플레이로 맞춘다.
    [SerializeField, Min(0f)] float showDistance = 6f;
    Transform player;
    IngredientShelf shelf;
    SharedFacility facility;
    Cafe cafe;
    Camera cameraView;
    UIMatchHudScreen hud;
    TransitionLedger ledger;
    float refreshAt;
    void Awake()
    {
        shelf = GetComponentInParent<IngredientShelf>();
        facility = GetComponentInParent<SharedFacility>();
        cafe = Cafe.Of(this);
        group.alpha = 0f;
    }
    // 그림은 HUD 층으로 옮겨 가 있어 진열대와 함께 파괴되지 않는다.
    void OnDestroy()
    {
        if (view != null) Destroy(view.gameObject);
    }
    void LateUpdate()
    {
        // 플레이·앱 종료 때는 HUD가 이 컴포넌트보다 먼저 파괴되며 그 층에 옮겨 둔 그림을 데려간다.
        if (view == null) return;
        var director = MatchDirector.Instance;
        var visible = director != null && director.Phase.Current == Phase.Day &&
            (cafe == null || cafe.TeamId == PlayerTeam.Local());
        if (cameraView == null) cameraView = Camera.main;
        if (hud == null) UIManager.Instance.TryGet(out hud);
        if (player == null) player = Unity.Netcode.NetworkManager.Singleton?.LocalClient?.PlayerObject?.transform;
        visible = visible && player != null && (cafe != null
                ? cafe.Floor != null && UICustomerOrder.InsideFloor(cafe.Floor.bounds, player.position)
                : (player.position - transform.position).sqrMagnitude <= showDistance * showDistance)
            && cameraView != null && hud != null && hud.PlaceMarker(view, transform.position, cameraView);
        group.alpha = visible ? 1f : 0f;
        if (!visible) return;
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 0.15f;
        if (ledger == null) ledger = director.GetComponent<TransitionLedger>();
        var item = shelf != null ? shelf.SlotItem(0) : facility != null ? SharedFacility.Gives(facility.Kind) : Ingredient.BreadBase;
        var count = shelf != null ? shelf.SlotCountAt(0) : -1;
        icon.Render(ResourceManager.Instance.IngredientSprite(item), count < 0 ? "상비" : count.ToString(), count != 0,
            ledger != null && System.Array.IndexOf(ledger.PopularShown, item) >= 0, item == Ingredient.BloodBean);
    }
}
