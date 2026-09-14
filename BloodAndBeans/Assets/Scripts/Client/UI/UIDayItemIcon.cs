using UnityEngine;
using TMPro;

/// 반복 아이콘 파츠. 상자와 주문은 같은 재료 그림을 쓴다 (5.7.2·5.7.3).
public sealed class UIDayItemIcon : MonoBehaviour
{
    [SerializeField] UnityEngine.UI.Image icon;
    [SerializeField] UnityEngine.UI.Image border;
    [SerializeField] TMP_Text count;
    [SerializeField] Color popular = new(1f, 0.78f, 0.25f);
    [SerializeField] Color blood = new(1f, 0.2f, 0.25f);
    [SerializeField, Range(0f, 1f)] float missingAlpha = 0.25f;
    public void Render(Sprite sprite, string quantity, bool available, bool highlighted, bool isBlood)
    {
        // 그림이 없으면 흰 사각형 대신 프레임만 남긴다.
        icon.enabled = sprite != null;
        icon.sprite = sprite;
        icon.color = new Color(1f, 1f, 1f, available ? 1f : missingAlpha);
        var edge = isBlood ? blood : highlighted ? popular : new Color(1f, 1f, 1f, 0.2f);
        if (!available) edge.a *= missingAlpha;
        border.color = edge;
        count.text = quantity;
        count.gameObject.SetActive(!string.IsNullOrEmpty(quantity));
    }
}
