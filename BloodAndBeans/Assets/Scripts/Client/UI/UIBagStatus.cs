using UnityEngine;
using UnityEngine.EventSystems;

/// 가방 미소지 X 표시와 재지급 안내 (기획서 6.8).
public sealed class UIBagStatus : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] GameObject missingMark;
    [SerializeField] GameObject tooltip;

    public void SetMissing(bool missing)
    {
        missingMark.SetActive(missing);
        if (gameObject.activeSelf != missing) gameObject.SetActive(missing);
        if (!missing) tooltip.SetActive(false);
    }

    public void OnPointerEnter(PointerEventData eventData) => tooltip.SetActive(true);
    public void OnPointerExit(PointerEventData eventData) => tooltip.SetActive(false);
    void OnDisable()
    {
        if (tooltip != null) tooltip.SetActive(false);
    }
}
