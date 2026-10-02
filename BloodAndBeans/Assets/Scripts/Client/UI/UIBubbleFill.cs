using UnityEngine;
using UnityEngine.UI;

/// 말풍선 이미지를 아래에서부터 채운다 (기획서 5.7.2 — 손님 인내심).
///
/// 칠하는 것은 `BB/UI Bubble Fill` 셰이더고, 이 컴포넌트는 채움 높이와 색을 정점에 실어 보낸다.
/// 머티리얼 대신 정점을 쓰는 이유는 <see cref="UIRoundImage"/>와 같다 — 손님마다 값이 달라도
/// 머티리얼 하나를 공유해 배칭이 깨지지 않는다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Image))]
public sealed class UIBubbleFill : BaseMeshEffect
{
    /// 스프라이트 높이 중 말풍선이 실제로 그려진 구간(바닥, 꼭대기). 투명 여백을 빼야
    /// 채움 0과 1이 말풍선 끝에 맞는다. `OrderBubble.png`의 알파에서 잰 값이다.
    [SerializeField] Vector2 range = new(0.075f, 0.923f);

    /// 정점이 나르는 두 값을 살리려면 캔버스가 이 채널을 켜야 한다 (<see cref="UIRoundImage"/>와 같다).
    const AdditionalCanvasShaderChannels FillChannels =
        AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;

    float fill;
    Color color = Color.clear;

    /// 출렁임 위상. 말풍선 넷이 같은 박자로 흔들리지 않게 켜질 때마다 하나 뽑는다.
    float phase;

    /// 채움 비율(0~1)과 색을 바꾼다. 값이 그대로면 메시를 다시 만들지 않는다.
    public void Show(float amount, Color tint)
    {
        amount = Mathf.Clamp01(amount);
        if (Mathf.Approximately(fill, amount) && color == tint) return;
        fill = amount;
        color = tint;
        graphic.SetVerticesDirty();
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        phase = Random.value * Mathf.PI * 2f;
        EnsureCanvasChannels();
    }

    /// 그림은 런타임에 HUD 표식 층으로 옮겨 가므로 부모가 바뀔 때 새 캔버스를 다시 본다.
    protected override void OnTransformParentChanged()
    {
        base.OnTransformParentChanged();
        EnsureCanvasChannels();
    }

    protected override void OnCanvasHierarchyChanged()
    {
        base.OnCanvasHierarchyChanged();
        EnsureCanvasChannels();
    }

    /// 높이 비율은 rect가 아니라 정점 범위로 잰다. `preserveAspect`면 그려지는 사각형이 rect보다 작다.
    public override void ModifyMesh(VertexHelper helper)
    {
        if (!IsActive() || helper.currentVertCount == 0) return;

        var vertex = new UIVertex();
        float bottom = float.MaxValue, top = float.MinValue, left = float.MaxValue, right = float.MinValue;
        for (var i = 0; i < helper.currentVertCount; i++)
        {
            helper.PopulateUIVertex(ref vertex, i);
            bottom = Mathf.Min(bottom, vertex.position.y);
            top = Mathf.Max(top, vertex.position.y);
            left = Mathf.Min(left, vertex.position.x);
            right = Mathf.Max(right, vertex.position.x);
        }

        // 0이면 선을 맨 아래로 내린다. 바닥 경계에 두면 그 밑 반투명 꼬리 끝이 물든다.
        var level = fill > 0f ? Mathf.Lerp(range.x, range.y, fill) : 0f;
        for (var i = 0; i < helper.currentVertCount; i++)
        {
            helper.PopulateUIVertex(ref vertex, i);
            vertex.uv1 = new Vector4(Mathf.InverseLerp(bottom, top, vertex.position.y), level,
                                     Mathf.InverseLerp(left, right, vertex.position.x), phase);
            vertex.uv2 = color;
            helper.SetUIVertex(vertex, i);
        }
    }

    void EnsureCanvasChannels()
    {
        var canvas = graphic != null ? graphic.canvas : null;
        if (canvas == null || (canvas.additionalShaderChannels & FillChannels) == FillChannels) return;

        canvas.additionalShaderChannels |= FillChannels;
    }
}
