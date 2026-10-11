using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// 후각 발동을 받은 로컬 카메라에만 짧은 화면 파동을 그린다.
public class TrackSenseFeature : FullScreenPassRendererFeature
{
    [SerializeField, Min(0.01f)] float pulseSeconds = 1.3f;
    static readonly int PulseTime = Shader.PropertyToID("_PulseTime");
    static readonly int PulseStrength = Shader.PropertyToID("_PulseStrength");

    Camera pulseCamera;
    float startedAt;
    Material runtimeMaterial;

    public void Pulse(Camera camera)
    {
        if (camera == null) return;
        pulseCamera = camera;
        startedAt = Time.time;
    }

    public void Clear(Camera camera)
    {
        if (pulseCamera == camera) pulseCamera = null;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!Application.isPlaying || pulseCamera == null || renderingData.cameraData.camera != pulseCamera) return;
        var elapsed = Time.time - startedAt;
        if (elapsed >= pulseSeconds) { pulseCamera = null; return; }
        if (passMaterial == null) return;

        // 아트 원본 머티리얼은 건드리지 않는다. 패스에만 런타임 사본을 넘긴다.
        if (runtimeMaterial == null) runtimeMaterial = new Material(passMaterial) { hideFlags = HideFlags.HideAndDontSave };
        var progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, pulseSeconds));
        runtimeMaterial.SetFloat(PulseTime, progress);
        runtimeMaterial.SetFloat(PulseStrength, Mathf.Sin(progress * Mathf.PI));
        var original = passMaterial;
        try
        {
            passMaterial = runtimeMaterial;
            base.AddRenderPasses(renderer, ref renderingData);
        }
        finally { passMaterial = original; }
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(runtimeMaterial);
        runtimeMaterial = null;
        pulseCamera = null;
        base.Dispose(disposing);
    }
}
