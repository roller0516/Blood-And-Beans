using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <see cref="UIRoundImage"/>가 정점에 싣는 값이 `BB/UI Rounded Rect` 셰이더의 약속과
/// 맞는지 본다. 반지름이 머티리얼이 아니라 정점으로 가기 때문에, 이 인코딩이 어긋나면
/// 화면에서는 "그냥 안 둥글다"로만 보이고 어디가 틀렸는지 드러나지 않는다.
public class UIRoundImageTests
{
    /// 셰이더의 `BB_RoundedBox`를 그대로 옮긴 것. 정점이 나르는 좌표계는 반지름이 1이다.
    static float RoundedBox(Vector2 p, Vector2 halfSize)
    {
        var q = new Vector2(Mathf.Abs(p.x), Mathf.Abs(p.y)) - halfSize + Vector2.one;
        return Vector2.Max(q, Vector2.zero).magnitude + Mathf.Min(Mathf.Max(q.x, q.y), 0f) - 1f;
    }

    /// 지정한 크기·반지름으로 사각형 하나를 만들고, 셰이더가 받게 될 값을 돌려준다.
    static (Vector2[] local, Vector2 halfSize) Bake(Vector2 size, float radius)
    {
        var go = new GameObject("round", typeof(RectTransform), typeof(Image), typeof(UIRoundImage));
        try
        {
            var rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = size;

            var round = go.GetComponent<UIRoundImage>();
            var serialized = new SerializedObject(round);
            serialized.FindProperty("radiusOverride").floatValue = radius;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // uGUI가 그리는 그대로의 네 정점. 원점은 RectTransform의 피벗이다.
            var helper = new VertexHelper();
            var r = rect.rect;
            foreach (var corner in new[]
                     {
                         new Vector2(r.xMin, r.yMin), new Vector2(r.xMin, r.yMax),
                         new Vector2(r.xMax, r.yMax), new Vector2(r.xMax, r.yMin)
                     })
                helper.AddVert(corner, Color.white, Vector2.zero);
            helper.AddTriangle(0, 1, 2);
            helper.AddTriangle(2, 3, 0);

            round.ModifyMesh(helper);

            var local = new Vector2[helper.currentVertCount];
            var vertex = new UIVertex();
            for (var i = 0; i < local.Length; i++)
            {
                helper.PopulateUIVertex(ref vertex, i);
                local[i] = vertex.uv1;
            }
            helper.PopulateUIVertex(ref vertex, 0);
            return (local, vertex.uv2);
        }
        finally { Object.DestroyImmediate(go); }
    }

    [Test]
    public void 모서리는_깎이고_한가운데는_남는다()
    {
        var (local, halfSize) = Bake(new Vector2(100f, 50f), 10f);

        // 반지름이 1인 좌표계이므로 100x50에 반지름 10이면 (5, 2.5)다.
        Assert.AreEqual(5f, halfSize.x, 1e-4f);
        Assert.AreEqual(2.5f, halfSize.y, 1e-4f);

        // 네 정점 모두 꼭짓점이라 사각형 밖이다 — 셰이더가 여기를 깎는다.
        foreach (var p in local)
            Assert.Greater(RoundedBox(p, halfSize), 0f, $"꼭짓점 {p}가 깎이지 않는다");

        Assert.Less(RoundedBox(Vector2.zero, halfSize), 0f, "한가운데가 깎인다");

        // 변 한가운데는 경계다. 여기가 어긋나면 판이 통째로 부풀거나 쪼그라든다.
        Assert.AreEqual(0f, RoundedBox(new Vector2(halfSize.x, 0f), halfSize), 1e-4f);
        Assert.AreEqual(0f, RoundedBox(new Vector2(0f, halfSize.y), halfSize), 1e-4f);
    }

    [Test]
    public void 반지름은_짧은_변의_절반에서_멈춘다()
    {
        // 넘긴 값을 그대로 쓰면 모서리 원 둘이 겹쳐 사각형이 안쪽으로 파인다.
        var (_, halfSize) = Bake(new Vector2(100f, 50f), 999f);

        // 반지름이 짧은 변의 절반인 25로 잘린다. 중심에서 변까지가 (50, 25)이므로
        // 반지름으로 나누면 (2, 1) — y가 1이면 위아래가 반원, 곧 알약이다.
        Assert.AreEqual(1f, halfSize.y, 1e-4f, "짧은 변이 반지름과 같아야 알약이 된다");
        Assert.AreEqual(2f, halfSize.x, 1e-4f);
    }

    [Test]
    public void 반지름이_0이면_음수를_실어_셰이더가_건드리지_않는다()
    {
        var (_, halfSize) = Bake(new Vector2(100f, 50f), 0f);

        Assert.Less(halfSize.x, 0f);
        Assert.Less(halfSize.y, 0f);
    }
}
