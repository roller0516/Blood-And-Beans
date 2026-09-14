using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// 조합법 한 줄. 복제된 내 팀 재고를 읽어 부족한 재료를 표시한다 (기획서 5.7.7).
public sealed class UIRecipeRow : MonoBehaviour
{
    [SerializeField] UINamedItemIcon cellPrefab;
    [SerializeField] Transform cells;
    [SerializeField] UINamedItemIcon product;
    [SerializeField] TMP_Text basePrice;
    [SerializeField] CanvasGroup availability;
    [SerializeField, Range(0f, 1f)] float unavailableAlpha = 0.4f;
    MenuDef menu;
    UINamedItemIcon[] ingredients;

    public void Bind(MenuDef definition)
    {
        menu = definition;
        ingredients = new UINamedItemIcon[menu.Parts.Length];
        for (var i = 0; i < ingredients.Length; i++)
            ingredients[i] = Instantiate(cellPrefab, cells);
        if (basePrice != null) basePrice.text = menu.BasePrice.ToString();
        Render(null, null);
    }

    public void Render(TeamStock stock, IReadOnlyList<Ingredient> popular)
    {
        var canMake = true;
        var resources = ResourceManager.Instance;
        for (var i = 0; i < ingredients.Length; i++)
        {
            var item = menu.Parts[i];
            var available = item == Ingredient.Bean || item == Menus.DessertBase
                || stock != null && stock.CountOf(item) > 0;
            var highlighted = false;
            if (popular != null)
                for (var j = 0; j < popular.Count; j++)
                    if (popular[j] == item) { highlighted = true; break; }
            ingredients[i].Show(resources.IngredientSprite(item), DisplayNames.Of(item),
                highlighted ? $"+{SalePrice.PopularBonus * 100f:0}%" : null,
                available, i > 0, highlighted);
            canMake &= available;
        }
        product.Show(resources.MenuSprite(menu.Id), DisplayNames.Of(menu.Id), null, canMake, false);
        if (availability != null) availability.alpha = canMake ? 1f : unavailableAlpha;
    }
}
