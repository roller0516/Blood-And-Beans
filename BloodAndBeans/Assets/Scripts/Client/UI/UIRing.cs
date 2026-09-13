using UnityEngine;

/// 낮 UI의 공통 링: 인내심·쿨다운·기간을 같은 시계 방향으로 읽는다 (5.7.1).
public sealed class UIRing : UnityEngine.UI.MaskableGraphic
{
    [SerializeField, Range(0f, 1f)] float amount = 1f;
    [SerializeField, Range(0.01f, 0.5f)] float thickness = 0.08f;
    public float Amount
    {
        get => amount;
        set { value = Mathf.Clamp01(value); if (Mathf.Approximately(amount, value)) return; amount = value; SetVerticesDirty(); }
    }
    protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper vh)
    {
        vh.Clear();
        var rect = GetPixelAdjustedRect();
        var radius = rect.size * 0.5f;
        // ponytail: 원 둘레 64분할. 작은 UI용이며 확대 시 분할 수를 늘린다.
        const int segments = 64;
        var count = Mathf.CeilToInt(segments * amount);
        for (var i = 0; i <= count; i++)
        {
            var angle = Mathf.Min((float)i / segments, amount) * Mathf.PI * 2f;
            var direction = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle));
            vh.AddVert(rect.center + Vector2.Scale(direction, radius), color, Vector2.zero);
            vh.AddVert(rect.center + Vector2.Scale(direction, radius) * (1f - thickness), color, Vector2.zero);
            if (i == 0) continue;
            var n = i * 2;
            vh.AddTriangle(n - 2, n, n + 1);
            vh.AddTriangle(n - 2, n + 1, n - 1);
        }
    }
}
