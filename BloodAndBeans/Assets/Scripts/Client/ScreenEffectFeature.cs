using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// 지정한 카메라에 정해진 시간 동안 전체 화면 머티리얼을 그린다. 효과마다 렌더러에 인스턴스를 하나씩 둔다.
/// 셰이더는 `_EffectProgress`(0→1 진행)·`_EffectStrength`(페이드 반영 세기)·`_EffectTime`(연출 시각)·
/// `_EffectElapsed`(켜진 뒤 지난 초. 켜질 때 연출용)를 받는다.
public class ScreenEffectFeature : FullScreenPassRendererFeature
{
    [SerializeField, Min(0.01f)] float fadeInSeconds = 0.25f;
    [SerializeField, Min(0.01f)] float fadeOutSeconds = 0.8f;
    #if UNITY_EDITOR
    [SerializeField] private bool bCheck;
    #endif
    static readonly int EffectProgress = Shader.PropertyToID("_EffectProgress");
    static readonly int EffectStrength = Shader.PropertyToID("_EffectStrength");
    static readonly int EffectTime = Shader.PropertyToID("_EffectTime");
    static readonly int EffectElapsed = Shader.PropertyToID("_EffectElapsed");

    Camera playCamera;
    float startedAt;
    float duration;
    Material runtimeMaterial;

#if UNITY_EDITOR
    Camera editorPreviewCamera;
    float editorPreviewStartedAt = -1f;

    /// 게임 실행 없이 Game 뷰에서 아트 조정을 미리 본다. 에셋에 저장하지 않는다.
    public void SetEditorPreview(Camera camera, bool enabled)
    {
        editorPreviewCamera = enabled ? camera : null;
        editorPreviewStartedAt = -1f;
        CoreUtils.Destroy(runtimeMaterial);
        runtimeMaterial = null;
    }
#endif

    /// 지속 시간은 기획 수치라 호출하는 쪽이 넘긴다.
    public void Play(Camera camera, float seconds)
    {
        if (camera == null || seconds <= 0f) return;
        playCamera = camera;
        startedAt = Time.time;
        duration = seconds;
    }

    public void Stop(Camera camera)
    {
        if (playCamera == camera) playCamera = null;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var preview = false;
        var camera = playCamera;
        var effectTime = Time.time;
#if UNITY_EDITOR
        if (Application.isPlaying || (!bCheck && editorPreviewCamera == null)) editorPreviewStartedAt = -1f;
        // 렌더러 Inspector의 체크박스를 켜면 Game 뷰 카메라에 바로 미리 본다.
        var previewCamera = editorPreviewCamera;
        if (bCheck && renderingData.cameraData.cameraType == CameraType.Game) previewCamera = renderingData.cameraData.camera;
        if (!Application.isPlaying && previewCamera != null)
        {
            preview = true;
            camera = previewCamera;
            effectTime = (float)UnityEditor.EditorApplication.timeSinceStartup;
            if (editorPreviewStartedAt < 0f) editorPreviewStartedAt = effectTime;
        }
#endif
        if ((!Application.isPlaying && !preview) || camera == null || renderingData.cameraData.camera != camera) return;
        var elapsed = Time.time - startedAt;
        if (!preview && elapsed >= duration) { playCamera = null; return; }
        if (passMaterial == null) return;

        // 아트 원본 머티리얼은 건드리지 않는다. 패스에만 런타임 사본을 넘긴다.
        if (runtimeMaterial == null) runtimeMaterial = new Material(passMaterial) { hideFlags = HideFlags.HideAndDontSave };
#if UNITY_EDITOR
        // 사본은 만든 순간의 값만 든다. Inspector에서 고친 값이 바로 보이게 매번 다시 옮긴다.
        runtimeMaterial.CopyPropertiesFromMaterial(passMaterial);
#endif
        var seconds = Mathf.Max(0.01f, duration);
        runtimeMaterial.SetFloat(EffectProgress, Mathf.Clamp01(elapsed / seconds));
        var strength = Mathf.Min(Mathf.Clamp01(elapsed / fadeInSeconds), Mathf.Clamp01((seconds - elapsed) / fadeOutSeconds));
        runtimeMaterial.SetFloat(EffectStrength, preview ? 1f : strength);
        runtimeMaterial.SetFloat(EffectTime, effectTime);
#if UNITY_EDITOR
        // 미리보기의 시작 연출은 한 번만 재생하고, 이후 바깥 효과를 유지한다.
        runtimeMaterial.SetFloat(EffectElapsed, preview ? effectTime - editorPreviewStartedAt : elapsed);
#else
        runtimeMaterial.SetFloat(EffectElapsed, elapsed);
#endif
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
        playCamera = null;
#if UNITY_EDITOR
        editorPreviewCamera = null;
        editorPreviewStartedAt = -1f;
#endif
        base.Dispose(disposing);
    }
}
