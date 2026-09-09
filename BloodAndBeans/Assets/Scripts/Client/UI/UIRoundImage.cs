using UnityEngine;
using UnityEngine.UI;

/// 이 오브젝트의 <see cref="Graphic"/> 하나를 둥근 모서리로 만든다.
///
/// uGUI의 Image에는 모서리 반지름이라는 것이 없다. 사각 메시라 둥글게 하려면 알파로 깎는
/// 수밖에 없어서, `BB/UI Rounded Rect` 셰이더가 모서리까지의 거리를 재서 깎는다.
///
/// 예전에는 내장 9-slice 스프라이트를 깔고 테두리 크기로 반지름을 만들었다. 그 방식은
/// **<see cref="Image.sprite"/> 자리를 라운드용 스프라이트가 차지해 버려서**, 자기 그림이
/// 있는 이미지(초상·썸네일·아이콘)는 애초에 둥글게 만들 수 없었다. 반지름이 커질수록
/// 가장자리가 흐려지는 문제도 같이 있었다. 셰이더는 그림을 그대로 두고 알파만 깎는다.
///
/// **둥글게 할 Graphic에 하나씩 붙인다.** 화면 루트에 붙여 자식을 훑지 않는 이유는, 화면을
/// 덮는 배경과 1px 구분선처럼 둥글면 안 되는 것을 코드가 크기로 짐작해야 하기 때문이다.
/// 어느 판이 둥글어야 하는지는 화면을 만든 사람이 안다.
///
/// 프리팹에 반지름을 박지 않고 <see cref="UIThemeConfig.CornerRadius"/>에서 읽는 것은
/// <see cref="UIFontScale"/>과 같은 이유다. 프리팹에 남는 것은 머티리얼 참조뿐이고,
/// 그것도 붙이는 순간 에디터가 테마에서 집어 넣는다.
///
/// 머티리얼을 <see cref="IMaterialModifier"/>로 갈아 끼우지 않고 <see cref="Graphic.material"/>에
/// 직렬화하는 이유는 마스크 때문이다. `StencilMaterial`이 *이 이미지의* 머티리얼을 스텐실
/// 값만 채워 복제하므로 마스크 안에서도 라운드가 유지된다. 렌더링 직전에 바꿔치면 그 복제가
/// 유니티 기본 UI 머티리얼을 대상으로 일어나 라운드가 사라진다.
[DisallowMultipleComponent]
[ExecuteInEditMode]
public class UIRoundImage : BaseMeshEffect
{
    /// 이 이미지만 다른 반지름을 쓸 때 채운다. 음수면 테마 값을 따른다.
    [SerializeField] float radiusOverride = -1f;

    /// 셰이더가 깎지 않게 하는 값. 반지름이 0일 때 정점에 실어 보낸다.
    static readonly Vector2 NoRounding = new(-1f, -1f);

    /// 정점이 나르는 두 값을 살려 두려면 캔버스가 이 채널들을 켜고 있어야 한다. 기본값은
    /// 위치·색·uv0뿐이라, 켜지 않으면 두 채널이 통째로 버려지고 모서리가 각진 채로 남는다.
    const AdditionalCanvasShaderChannels RoundChannels =
        AdditionalCanvasShaderChannels.TexCoord1 | AdditionalCanvasShaderChannels.TexCoord2;

    public float Radius => radiusOverride >= 0f ? radiusOverride : UITheme.Config.CornerRadius;

    protected override void OnEnable()
    {
        base.OnEnable();
        EnsureCanvasChannels();
    }

    /// 캔버스를 갈아탔다 — 화면을 만들어 캔버스 밑에 붙이는 경우가 이것이다. `OnEnable`은
    /// 붙기 전에 이미 지나갔고 그때는 캔버스가 없었으므로, 여기서 한 번 더 봐야 한다.
    ///
    /// `OnCanvasHierarchyChanged`로는 모자란다. 그건 캔버스 컴포넌트가 켜지고 꺼질 때만
    /// 오고 부모가 바뀌는 것으로는 오지 않는다 (에디터에서 확인).
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

    /// 테마 값이 바뀌었다 (`UIThemeGroup.ApplyLive`). 반지름은 정점에 실리므로 다시 그리게
    /// 하는 것이 곧 다시 먹이는 것이다.
    public void Apply()
    {
        EnsureCanvasChannels();
        if (graphic != null) graphic.SetVerticesDirty();
    }

    /// 정점마다 "반지름을 1로 놓은 좌표계"의 위치와, 그 좌표계에서 중심부터 변까지의 거리를
    /// 실어 보낸다. 나눗셈을 여기서 한 번에 끝내면 셰이더는 재기만 하면 된다.
    ///
    /// 반지름은 짧은 변의 절반을 넘을 수 없다. 넘기면 모서리 원 둘이 겹쳐 사각형이 안쪽으로
    /// 파이므로, 넘긴 값은 알약 모양이 되는 데서 멈춘다.
    public override void ModifyMesh(VertexHelper helper)
    {
        if (!IsActive() || graphic == null) return;

        var rect = graphic.rectTransform.rect;
        var radius = Mathf.Min(Radius, Mathf.Min(rect.width, rect.height) * 0.5f);

        var halfSize = NoRounding;
        var scale = 0f;
        if (radius > 0f)
        {
            scale = 1f / radius;
            halfSize = rect.size * (0.5f * scale);
        }

        var center = rect.center;
        var vertex = new UIVertex();
        for (var i = 0; i < helper.currentVertCount; i++)
        {
            helper.PopulateUIVertex(ref vertex, i);

            // 9-slice나 타일이면 정점이 넷보다 많다. 위치에서 재므로 몇 개든 상관없다.
            vertex.uv1 = ((Vector2)vertex.position - center) * scale;
            vertex.uv2 = halfSize;
            helper.SetUIVertex(vertex, i);
        }
    }

    /// ponytail: 편집 중에 켜면 캔버스가 dirty로 잡혀 씬·프리팹에 저장을 요구한다. 한 번
    /// 저장되면 아래 검사에 걸려 다시는 건드리지 않으므로 그대로 둔다.
    void EnsureCanvasChannels()
    {
        var canvas = graphic != null ? graphic.canvas : null;
        if (canvas == null || (canvas.additionalShaderChannels & RoundChannels) == RoundChannels) return;

        canvas.additionalShaderChannels |= RoundChannels;
    }

#if UNITY_EDITOR
    /// 붙이기만 하면 되게 한다 — 머티리얼이 비어 있거나 유니티 기본 UI 머티리얼이면 테마의
    /// 것으로 갈아 끼운다. 일부러 다른 머티리얼을 이어 둔 경우는 건드리지 않는다.
    protected override void OnValidate()
    {
        base.OnValidate();
        if (graphic == null) return;

        var rounded = UITheme.Config.RoundedMaterial;
        if (rounded == null)
        {
            CDebug.LogError($"{name}: 테마에 둥근 모서리 머티리얼이 없어 각진 채로 둔다. "
                          + "UIThemeConfig의 '모서리'에 BB/UI Rounded Rect 머티리얼을 이어야 한다.", this);
            return;
        }

        if (graphic.material == null || graphic.material == graphic.defaultMaterial)
        {
            graphic.material = rounded;
            UnityEditor.EditorUtility.SetDirty(graphic);
        }

        Apply();
    }
#endif
}
