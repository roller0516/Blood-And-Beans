using UnityEngine;

/// 재료대는 재고가 없어도 남기고 배지만 흐린다 (5.7.3).
public sealed class UIIngredientBadge : MonoBehaviour
{
    [SerializeField] Canvas canvas;
    [SerializeField] UIDayItemIcon icon;
    [SerializeField] UIThemeConfig theme;
    IngredientShelf shelf;
    SharedFacility facility;
    Cafe cafe;
    Camera cameraView;
    TransitionLedger ledger;
    float refreshAt;
    void Awake()
    {
        shelf = GetComponentInParent<IngredientShelf>();
        facility = GetComponentInParent<SharedFacility>();
        cafe = Cafe.Of(this);
    }
    void LateUpdate()
    {
        var director = MatchDirector.Instance;
        canvas.enabled = director != null && director.Phase.Current == Phase.Day &&
            (cafe == null || cafe.TeamId == PlayerTeam.Local());
        if (!canvas.enabled) return;
        if (cameraView == null) cameraView = Camera.main;
        if (cameraView != null) transform.forward = cameraView.transform.forward;
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + 0.15f;
        if (ledger == null) ledger = director.GetComponent<TransitionLedger>();
        var item = shelf != null ? shelf.SlotItem(0) : facility != null && facility.Kind == FacilityKind.Beans ? Ingredient.Bean : Ingredient.BreadBase;
        var count = shelf != null ? shelf.SlotCountAt(0) : -1;
        icon.Render(theme.DaySprite(item), count < 0 ? "상비" : count.ToString(), count != 0,
            ledger != null && System.Array.IndexOf(ledger.PopularShown, item) >= 0, item == Ingredient.BloodBean);
    }
}
