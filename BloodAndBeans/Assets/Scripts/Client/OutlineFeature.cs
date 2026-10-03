using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// `TargetOutline`이 렌더링 레이어 비트를 켠 렌더러만 마스크·테두리 머티리얼로 한 번 더 그린다.
/// 테두리 원리는 `Art/Shaders/StationOutline.shader`에 있다. `PC_Renderer`에 붙는다.
public class OutlineFeature : ScriptableRendererFeature
{
    /// 스텐실만 찍는 벌. `StationOutlineMask.mat`.
    [SerializeField] Material maskMaterial;

    /// 테두리 벌. `StationOutline.mat`. 색과 두께는 이 머티리얼이 갖는다.
    [SerializeField] Material outlineMaterial;

    [Header("다음에 집힐 아이템 (ItemSlotPresenter.NextPickBit)")]
    [Tooltip("스텐실 비트가 설비 테두리와 달라야 한다. 같으면 설비 마스크가 아이템 테두리를 지운다.")]
    [SerializeField] Material nextMaskMaterial;
    [SerializeField] Material nextOutlineMaterial;

    [SerializeField] RenderPassEvent passEvent = RenderPassEvent.AfterRenderingTransparents;

    OutlinePass pass;

    public override void Create() => pass = new OutlinePass { renderPassEvent = passEvent };

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (maskMaterial == null || outlineMaterial == null) return;
        if (renderingData.cameraData.cameraType is CameraType.Preview or CameraType.Reflection) return;

        pass.Setup(maskMaterial, outlineMaterial, nextMaskMaterial, nextOutlineMaterial);
        renderer.EnqueuePass(pass);
    }

    class OutlinePass : ScriptableRenderPass
    {
        static readonly List<ShaderTagId> ShaderTags = new()
        {
            new ShaderTagId("UniversalForward"),
            new ShaderTagId("UniversalForwardOnly"),
            new ShaderTagId("SRPDefaultUnlit"),
        };

        Material mask;
        Material outline;
        Material nextMask;
        Material nextOutline;

        class PassData
        {
            public RendererListHandle Mask;
            public RendererListHandle Outline;
            public bool HasNext;
            public RendererListHandle NextMask;
            public RendererListHandle NextOutline;
        }

        public void Setup(Material maskMaterial, Material outlineMaterial, Material nextMaskMaterial, Material nextOutlineMaterial)
        {
            mask = maskMaterial;
            outline = outlineMaterial;
            nextMask = nextMaskMaterial;
            nextOutline = nextOutlineMaterial;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var resources = frameData.Get<UniversalResourceData>();
            var rendering = frameData.Get<UniversalRenderingData>();
            var camera = frameData.Get<UniversalCameraData>();
            var light = frameData.Get<UniversalLightData>();

            using var builder = renderGraph.AddRasterRenderPass<PassData>("Target Outline", out var data);

            data.Mask = RendererList(renderGraph, rendering, camera, light, mask, TargetOutline.RenderingLayerBit);
            data.Outline = RendererList(renderGraph, rendering, camera, light, outline, TargetOutline.RenderingLayerBit);
            builder.UseRendererList(data.Mask);
            builder.UseRendererList(data.Outline);

            data.HasNext = nextMask != null && nextOutline != null;
            data.NextMask = data.HasNext ? RendererList(renderGraph, rendering, camera, light, nextMask, ItemSlotPresenter.NextPickBit) : default;
            data.NextOutline = data.HasNext ? RendererList(renderGraph, rendering, camera, light, nextOutline, ItemSlotPresenter.NextPickBit) : default;
            if (data.HasNext)
            {
                builder.UseRendererList(data.NextMask);
                builder.UseRendererList(data.NextOutline);
            }

            // 스텐실을 쓰고 읽으므로 깊이·스텐실 버퍼를 같이 묶는다.
            builder.SetRenderAttachment(resources.activeColorTexture, 0);
            builder.SetRenderAttachmentDepth(resources.activeDepthTexture, AccessFlags.ReadWrite);

            // 마스크가 먼저 비트를 찍어야 테두리가 그 자리를 뺀다. 다음 아이템은 나중에 그려 설비 테두리 위에 온다.
            builder.SetRenderFunc(static (PassData d, RasterGraphContext context) =>
            {
                context.cmd.DrawRendererList(d.Mask);
                context.cmd.DrawRendererList(d.Outline);
                if (!d.HasNext) return;
                context.cmd.DrawRendererList(d.NextMask);
                context.cmd.DrawRendererList(d.NextOutline);
            });
        }

        static RendererListHandle RendererList(RenderGraph renderGraph, UniversalRenderingData rendering,
            UniversalCameraData camera, UniversalLightData light, Material material, uint renderingLayer)
        {
            var drawing = RenderingUtils.CreateDrawingSettings(ShaderTags, rendering, camera, light, SortingCriteria.CommonOpaque);
            drawing.overrideMaterial = material;

            var filtering = new FilteringSettings(RenderQueueRange.all) { renderingLayerMask = renderingLayer };
            return renderGraph.CreateRendererList(new RendererListParams(rendering.cullResults, drawing, filtering));
        }
    }
}
