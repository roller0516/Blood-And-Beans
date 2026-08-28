using UnityEditor;
using UnityEngine;

/// 편집 중에 안개 전역 값을 0으로 눕혀 둔다.
///
/// 안개는 전역 셰이더 값으로 그려지고(`FogRenderer`), 전역 값은 플레이를 멈춰도 남는다.
/// 남은 값이 있으면 Scene View와 Game View가 계속 안개색으로 덮여서, 씬을 편집할 때
/// 아무것도 안 보인다. `FogRenderer.OnDestroy`가 플레이 종료 시 지우지만 그것만으로는
/// 부족하다 — 에디터를 껐다 켜거나, 플레이 없이 값만 건드린 경우가 남는다.
[InitializeOnLoad]
static class FogGlobalsGuard
{
    static readonly int FogColour = Shader.PropertyToID("_BB_FogColor");

    static FogGlobalsGuard()
    {
        Clear();
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) Clear();
        };
    }

    /// 알파만 0으로 만든다. 색은 남겨 두어도 그려지지 않는다.
    static void Clear() => Shader.SetGlobalColor(FogColour, Color.clear);
}
