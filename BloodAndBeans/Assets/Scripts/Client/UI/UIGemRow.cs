using TMPro;
using UnityEngine;

/// 자동 업그레이드 한 줄 — 보석 아이콘, 「이름 — 효과」, 남은 턴 (기획서 4.1).
public sealed class UIGemRow : MonoBehaviour
{
    [SerializeField] UIGemIcon icon;
    [SerializeField] TMP_Text title;
    [SerializeField] TMP_Text turns;

    public void Render(Sprite sprite, int remaining, string name, string effect, bool refreshed)
    {
        icon.Render(sprite, remaining);
        title.text = $"{name} — {effect}";
        // 기획서 4.1: 갱신된 보석은 「3턴으로 갱신」으로 표기한다.
        turns.text = refreshed ? $"{remaining}턴으로 갱신" : $"{remaining}턴";
    }
}
