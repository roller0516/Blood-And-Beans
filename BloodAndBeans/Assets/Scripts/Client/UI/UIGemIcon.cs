using UnityEngine;

/// HUD 좌측 세로에 쌓이는 보석 한 칸 (기획서 5.7.1). 숫자 없이 테두리 링이 남은 턴만큼
/// 줄고(3 → 2 → 1), 마지막 턴에만 깜빡인다.
public sealed class UIGemIcon : MonoBehaviour
{
    [SerializeField] UnityEngine.UI.Image icon;
    /// Filled · Radial360 링.
    [SerializeField] UnityEngine.UI.Image ring;
    [SerializeField] CanvasGroup group;
    [SerializeField, Min(0.1f)] float blinksPerSecond = 1.5f;
    [SerializeField, Range(0f, 1f)] float blinkMinAlpha = 0.3f;

    int shownTurns = -1;

    void Awake() => enabled = false;

    public void Render(Sprite sprite, int turns)
    {
        if (icon.sprite != sprite) icon.sprite = sprite;
        if (turns == shownTurns) return;
        shownTurns = turns;
        ring.fillAmount = Mathf.Clamp01(turns / (float)Gems.Turns);
        // 깜빡임은 마지막 턴일 때만 돈다. 그 밖에는 Update가 꺼져 있다.
        enabled = turns == 1;
        if (!enabled) group.alpha = 1f;
    }

    void Update() =>
        group.alpha = Mathf.Lerp(blinkMinAlpha, 1f, Mathf.PingPong(Time.unscaledTime * blinksPerSecond * 2f, 1f));

    void OnDisable() => group.alpha = 1f;
}
