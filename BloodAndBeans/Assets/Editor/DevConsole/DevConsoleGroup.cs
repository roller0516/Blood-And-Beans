using UnityEngine.UIElements;

/// 개발 콘솔의 한 그룹. 창은 그룹이 무엇을 하는지 모르고, 그룹은 탭이 어떻게 그려지는지
/// 모른다. 그래서 그룹을 더해도 창은 그대로다.
///
/// <b>새 그룹을 만들려면:</b>
/// <list type="number">
/// <item>`DevConsoleGroup`을 상속한 파일을 `Editor/DevConsole/Groups/`에 만든다.</item>
/// <item><see cref="Tab"/>과 <see cref="Title"/>을 정하고 <see cref="Build"/>·<see cref="Refresh"/>를 채운다.
///       같은 <see cref="Tab"/> 이름을 쓰면 기존 탭에 얹히고, 새 이름이면 탭이 하나 생긴다.</item>
/// <item><see cref="DevConsoleWindow"/>의 `groups` 배열에 한 줄 더한다. 배열 순서가 곧 화면 순서다.</item>
/// </list>
///
/// 생김새는 `DevConsoleWindow.uss`에 있다. 여기 도우미는 그 클래스 이름을 붙여 줄 뿐이다.
public abstract class DevConsoleGroup
{
    /// 이 그룹이 들어갈 탭 이름.
    public abstract string Tab { get; }

    /// 그룹 상자에 붙는 제목.
    public abstract string Title { get; }

    /// 창이 부른다. 제목 붙은 상자를 만들어 <see cref="Build"/>에 넘긴다.
    public void Attach(VisualElement parent) => Build(MakeGroup(parent, Title));

    /// 상자 안을 채운다. 만든 요소는 필드에 들고 있다가 <see cref="Refresh"/>에서 갱신한다.
    protected abstract void Build(VisualElement group);

    /// 주기적으로 불린다(약 10Hz). 씬 오브젝트가 아직 없을 수 있으니 항상 null을 확인한다.
    public abstract void Refresh(in DevConsoleState state);

    // ── 조립 도우미 ────────────────────────────────────────────────

    /// 제목 붙은 상자 하나.
    protected static VisualElement MakeGroup(VisualElement parent, string title)
    {
        var group = new VisualElement();
        group.AddToClassList("group");

        var label = new Label(title);
        label.AddToClassList("group__title");
        group.Add(label);

        parent.Add(group);
        return group;
    }

    /// 라벨 + 값 한 줄. 돌려주는 것은 값 쪽 Label이라 갱신할 때 그것만 들고 있으면 된다.
    protected static Label Row(VisualElement parent, string label, string value)
    {
        var row = new VisualElement();
        row.AddToClassList("row");

        var name = new Label(label);
        name.AddToClassList("row__label");
        var content = new Label(value);
        content.AddToClassList("row__value");

        row.Add(name);
        row.Add(content);
        parent.Add(row);
        return content;
    }

    /// 라벨 + 입력 칸 한 줄.
    protected static IntegerField FieldRow(VisualElement parent, string label, int value)
    {
        var row = new VisualElement();
        row.AddToClassList("row");

        var name = new Label(label);
        name.AddToClassList("row__label");
        var field = new IntegerField { value = value };
        field.AddToClassList("row__field");

        row.Add(name);
        row.Add(field);
        parent.Add(row);
        return field;
    }

    /// 라벨 + 소수 입력 칸 한 줄.
    protected static FloatField FloatRow(VisualElement parent, string label, float value)
    {
        var row = new VisualElement();
        row.AddToClassList("row");

        var name = new Label(label);
        name.AddToClassList("row__label");
        var field = new FloatField { value = value };
        field.AddToClassList("row__field");

        row.Add(name);
        row.Add(field);
        parent.Add(row);
        return field;
    }

    /// 버튼을 가로로 늘어놓는 줄. 버튼은 이 줄 안에서 폭을 나눠 가진다.
    protected static VisualElement ButtonRow(VisualElement parent)
    {
        var row = new VisualElement();
        row.AddToClassList("buttons");
        parent.Add(row);
        return row;
    }

    protected static Button Btn(VisualElement parent, string text,
                                System.Action action, string extraClass = null)
    {
        var button = new Button(action) { text = text };
        button.AddToClassList("btn");
        if (extraClass != null) button.AddToClassList(extraClass);
        parent.Add(button);
        return button;
    }
}
