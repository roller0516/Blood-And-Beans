using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// 마우스 회전 감도. 값을 새로 만들지 않는다 — `LookSensitivity`가 이미 배수를 들고
/// `PlayerPrefs`에 저장하며, 이 그룹은 그 값을 재생 중에 만져 보게 해 줄 뿐이다.
///
/// 감도는 플레이어와 함께 스폰되므로(`PlayerCameraRoot`) 재생 중에만 대상이 있다.
/// 기준값(`lookGain`) 자체를 바꾸려면 프리팹에서 `PlayerCameraRoot`를 연다.
public class LookSensitivityGroup : DevConsoleGroup
{
    public override string Tab => "치트";
    public override string Title => "마우스 감도";

    FloatField multiplier;
    Button apply;
    Label scope;

    LookSensitivity look;
    bool lastPlaying;
    bool filled;

    protected override void Build(VisualElement group)
    {
        var hint = new Label("재생 중에만 대상이 있다. 값은 PlayerPrefs에 남는다.");
        hint.AddToClassList("hint");
        group.Add(hint);

        scope = Row(group, "대상", "-");
        multiplier = FloatRow(group, "배수", LookSensitivity.Default);

        apply = Btn(ButtonRow(group), "적용", Apply);
    }

    public override void Refresh(in DevConsoleState state)
    {
        // 재생을 넘나들면 씬 인스턴스가 통째로 바뀐다. 그 외에는 다시 찾지 않는다.
        if (lastPlaying != state.Playing || look == null)
        {
            lastPlaying = state.Playing;
            look = Object.FindAnyObjectByType<LookSensitivity>();
            filled = false;
        }

        scope.text = look == null ? "없음 (플레이어 스폰 전)" : look.gameObject.name;
        apply.SetEnabled(look != null);

        // 한 번만 채운다. 매 갱신마다 덮어쓰면 타이핑하는 중에 숫자가 되돌아간다.
        if (!filled && look != null)
        {
            multiplier.SetValueWithoutNotify(look.Multiplier);
            filled = true;
        }
    }

    void Apply()
    {
        if (look == null) return;
        look.Apply(Mathf.Clamp(multiplier.value, LookSensitivity.Min, LookSensitivity.Max));
        multiplier.SetValueWithoutNotify(look.Multiplier);
    }
}
