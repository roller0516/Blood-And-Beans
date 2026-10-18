using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

/// `UIMatchHudScreen` 프리팹을 프리팹 모드로 열었을 때만 씬 뷰에 뜨는 낮/밤 버튼.
/// 전환은 `UIMatchHudScreen.ShowPhase`를 그대로 부른다. 누르면 두 루트의 활성 상태가 프리팹에
/// 같이 저장되지만, 실제 게임은 첫 `Render`가 다시 맞추므로 동작에는 영향이 없다.
[Overlay(typeof(SceneView), "HUD 낮/밤", defaultDisplay = false)]
public sealed class UIMatchHudPhaseOverlay : Overlay
{
    public override void OnCreated()
    {
        PrefabStage.prefabStageOpened += OnStageOpened;
        PrefabStage.prefabStageClosing += OnStageClosing;
        displayed = FindHud(PrefabStageUtility.GetCurrentPrefabStage()) != null;
    }

    public override void OnWillBeDestroyed()
    {
        PrefabStage.prefabStageOpened -= OnStageOpened;
        PrefabStage.prefabStageClosing -= OnStageClosing;
    }

    void OnStageOpened(PrefabStage stage) => displayed = FindHud(stage) != null;

    void OnStageClosing(PrefabStage _) => displayed = false;

    public override VisualElement CreatePanelContent()
    {
        var root = new VisualElement { style = { flexDirection = FlexDirection.Row } };
        root.Add(new Button(() => Show(true)) { text = "낮" });
        root.Add(new Button(() => Show(false)) { text = "밤" });
        return root;
    }

    static UIMatchHudScreen FindHud(PrefabStage stage) =>
        stage != null ? stage.prefabContentsRoot.GetComponent<UIMatchHudScreen>() : null;

    static void Show(bool isDay)
    {
        var hud = FindHud(PrefabStageUtility.GetCurrentPrefabStage());
        if (hud == null) return;

        hud.ShowPhase(isDay);
        SceneView.RepaintAll();
    }
}
