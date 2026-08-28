using UnityEngine;
using UnityEngine.UIElements;

/// 시점 전환. 쿼터뷰와 TPP를 같은 판에서 번갈아 보며 비교하려고 둔 것이라, 게임 규칙과는
/// 무관하고 서버에도 아무것도 보내지 않는다 — 보는 사람의 화면만 바뀐다.
///
/// 밤이 아닐 때 눌러도 된다. 카페 고정 뷰는 시점 선택과 무관하고, 밤이 되면 고른 쪽이 뜬다.
public class CameraGroup : DevConsoleGroup
{
    public override string Tab => "치트";
    public override string Title => "시점";

    Label current;
    Button quarterButton, tppButton;

    /// 매치 씬과 함께 생겼다 사라진다. 재생을 벗어나면 놓고, 찾을 때까지만 찾는다.
    MatchCameraDirector camera;

    protected override void Build(VisualElement group)
    {
        var hint = new Label("보는 사람 화면만 바뀐다. 밤에 적용된다.");
        hint.AddToClassList("hint");
        group.Add(hint);

        current = Row(group, "지금", "-");

        var buttons = ButtonRow(group);
        quarterButton = Btn(buttons, "쿼터뷰", () => Apply(MatchCameraDirector.PlayerView.Quarter));
        tppButton = Btn(buttons, "TPP", () => Apply(MatchCameraDirector.PlayerView.ThirdPerson));
    }

    public override void Refresh(in DevConsoleState state)
    {
        if (!state.Playing)
        {
            // 재생을 벗어나면 파괴된 오브젝트를 붙들고 있지 않는다.
            camera = null;
            current.text = "-";
            quarterButton.SetEnabled(false);
            tppButton.SetEnabled(false);
            return;
        }

        if (camera == null) camera = Object.FindAnyObjectByType<MatchCameraDirector>();

        var found = camera != null;
        current.text = !found ? "매치 씬 아님" : ViewName(camera.View);
        quarterButton.SetEnabled(found && camera.View != MatchCameraDirector.PlayerView.Quarter);
        tppButton.SetEnabled(found && camera.View != MatchCameraDirector.PlayerView.ThirdPerson);
    }

    void Apply(MatchCameraDirector.PlayerView view)
    {
        if (camera != null) camera.SetView(view);
    }

    static string ViewName(MatchCameraDirector.PlayerView view) =>
        view == MatchCameraDirector.PlayerView.Quarter ? "쿼터뷰" : "TPP";
}
