using UnityEditor;
using UnityEditor.Overlays;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

/// 프리팹 모드로 연 `UIScreen`이 `PreviewStates`를 선언했을 때만 씬 뷰에 뜨는 버튼 모음.
/// 화면 타입은 모른다 — 상태 이름과 동작은 화면이 정한다.
/// 누른 상태는 프리팹에 그대로 저장될 수 있다. 실제 게임은 화면이 뜰 때 다시 맞추므로 동작에는 영향이 없다.
[Overlay(typeof(SceneView), "화면 미리보기", defaultDisplay = false)]
public sealed class UIScreenPreviewOverlay : Overlay
{
    readonly VisualElement buttons = new() { style = { flexDirection = FlexDirection.Row } };

    public override void OnCreated()
    {
        PrefabStage.prefabStageOpened += Rebuild;
        PrefabStage.prefabStageClosing += OnStageClosing;
        Rebuild(PrefabStageUtility.GetCurrentPrefabStage());
    }

    public override void OnWillBeDestroyed()
    {
        PrefabStage.prefabStageOpened -= Rebuild;
        PrefabStage.prefabStageClosing -= OnStageClosing;
    }

    public override VisualElement CreatePanelContent() => buttons;

    void OnStageClosing(PrefabStage _) => Rebuild(null);

    /// 이 프리팹의 상태로 버튼을 새로 만든다. 상태가 없으면 오버레이를 숨긴다.
    void Rebuild(PrefabStage stage)
    {
        buttons.Clear();
        var screen = stage != null ? stage.prefabContentsRoot.GetComponent<UIScreen>() : null;
        if (screen != null)
        {
            foreach (var state in screen.PreviewStates)
            {
                var apply = state.Apply;
                buttons.Add(new Button(() => { apply(); SceneView.RepaintAll(); }) { text = state.Label });
            }
        }
        displayed = buttons.childCount > 0;
    }
}
