using TMPro;
using UnityEngine;

/// 프레임 아이콘 아래 이름을 적은 칸. 조합식의 재료·완성품과 손님 말풍선이 같이 쓴다.
/// 조합식에서 앞 칸이 있으면 「+」를 붙인다.
public sealed class UINamedItemIcon : MonoBehaviour
{
    [SerializeField] GameObject plus;
    [SerializeField] UIDayItemIcon icon;
    [SerializeField] TMP_Text label;

    public void Bind(Ingredient item, bool leadingPlus) =>
        Show(ResourceManager.Instance.IngredientSprite(item), DisplayNames.Of(item), null, true, leadingPlus);

    public void Show(Sprite sprite, string itemName, string count, bool available, bool leadingPlus, bool highlighted = false)
    {
        plus.SetActive(leadingPlus);
        icon.Render(sprite, count, available, highlighted, false);
        label.text = itemName;
    }
}
