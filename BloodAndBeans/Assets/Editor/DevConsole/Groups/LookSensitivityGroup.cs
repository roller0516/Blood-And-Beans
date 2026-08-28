using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

/// 마우스 회전 감도. 값을 새로 만들지 않는다 — Cinemachine의
/// `CinemachineInputAxisController`가 이미 축마다 `Input.Gain`을 들고 있고, 이 그룹은
/// Inspector의 중첩 배열 안에 묻혀 있는 그 값을 한 곳으로 끌어낼 뿐이다.
///
/// 재생 중에 넣으면 즉시 느낌이 바뀌지만 재생을 끄면 되돌아간다 — Cinemachine이 축 목록을
/// `[NoSaveDuringPlay]`로 표시해 두었기 때문이다. 재생을 끄고 넣으면 씬에 저장된다.
/// 그래서 쓰는 순서는 "재생하며 값을 찾고, 끄고 다시 넣는다"다.
///
/// 지금 감도를 가진 카메라는 TPP 하나다. 쿼터뷰는 마우스가 개입하지 않도록 입력
/// 컨트롤러를 떼어 냈다(`MatchCameraBuilder`).
public class LookSensitivityGroup : DevConsoleGroup
{
    public override string Tab => "치트";
    public override string Title => "마우스 감도";

    /// Cinemachine이 축에 붙이는 이름의 끝글자. "Look Orbit X"·"Look Orbit Y"가 이 규칙이며,
    /// 반경 축인 "Orbit Scale"은 어느 쪽에도 걸리지 않아 그대로 남는다.
    const string HorizontalSuffix = " X";
    const string VerticalSuffix = " Y";

    FloatField horizontal, vertical;
    Button apply;
    Label scope;

    CinemachineInputAxisController[] controllers = System.Array.Empty<CinemachineInputAxisController>();
    bool lastPlaying;
    bool filled;

    protected override void Build(VisualElement group)
    {
        var hint = new Label("재생 중에는 즉시, 재생을 끄고 넣으면 씬에 저장된다.");
        hint.AddToClassList("hint");
        group.Add(hint);

        scope = Row(group, "대상", "-");
        horizontal = FloatRow(group, "가로", 0f);
        vertical = FloatRow(group, "세로", 0f);

        apply = Btn(ButtonRow(group), "적용", Apply);
    }

    public override void Refresh(in DevConsoleState state)
    {
        // 재생을 넘나들면 씬 인스턴스가 통째로 바뀐다. 그 외에는 다시 찾지 않는다.
        if (lastPlaying != state.Playing || controllers.Length == 0 || controllers[0] == null)
        {
            lastPlaying = state.Playing;
            controllers = Object.FindObjectsByType<CinemachineInputAxisController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            filled = false;
        }

        var axes = CountAxes();
        scope.text = axes == 0 ? "없음" : $"카메라 {controllers.Length} · 축 {axes}";
        apply.SetEnabled(axes > 0);

        // 한 번만 채운다. 매 갱신마다 덮어쓰면 타이핑하는 중에 숫자가 되돌아간다.
        if (!filled && axes > 0) Fill();
    }

    int CountAxes()
    {
        var axes = 0;
        foreach (var controller in controllers)
        {
            if (controller == null) continue;
            foreach (var axis in controller.Controllers)
                if (IsLookAxis(axis.Name)) axes++;
        }
        return axes;
    }

    static bool IsLookAxis(string name) =>
        name.EndsWith(HorizontalSuffix) || name.EndsWith(VerticalSuffix);

    /// 지금 씬에 들어 있는 값을 입력 칸에 옮긴다. 처음 보는 사람이 "지금 얼마인가"를
    /// 알아야 얼마나 올릴지 정할 수 있다.
    void Fill()
    {
        foreach (var controller in controllers)
        {
            if (controller == null) continue;
            foreach (var axis in controller.Controllers)
            {
                if (axis.Name.EndsWith(HorizontalSuffix)) horizontal.value = axis.Input.Gain;
                else if (axis.Name.EndsWith(VerticalSuffix)) vertical.value = axis.Input.Gain;
            }
        }
        filled = true;
    }

    void Apply()
    {
        foreach (var controller in controllers)
        {
            if (controller == null) continue;

            if (!lastPlaying) Undo.RecordObject(controller, "마우스 감도");

            foreach (var axis in controller.Controllers)
            {
                if (axis.Name.EndsWith(HorizontalSuffix)) axis.Input.Gain = horizontal.value;
                else if (axis.Name.EndsWith(VerticalSuffix)) axis.Input.Gain = vertical.value;
            }

            if (lastPlaying) continue;

            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
        }
    }
}
