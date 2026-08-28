using UnityEngine;
using UnityEngine.UIElements;

/// 페이즈 치트. 마감 시각만 당기고 실제 전이는 `GamePhase.Update`의 정규 경로가 한다.
/// 그래야 전환 중 임대료 정산과 `PhaseEntered` 구독자가 실제 진행과 똑같이 돈다 —
/// 치트로 넘긴 하루와 기다린 하루의 결과가 같아야 테스트에 의미가 있다.
///
/// 시계는 서버 권위라 서버(호스트)에서만 조작이 열린다. 클라이언트에서 눌러 봐야 서버
/// 상태는 그대로이고, 그걸 모른 채 테스트하면 없는 결함을 쫓게 된다.
public class PhaseCheatGroup : DevConsoleGroup
{
    public override string Tab => "치트";
    public override string Title => "진행";

    Label title, time, warning;
    VisualElement fill;
    Button endPhase, nextDay;
    GamePhase clock;

    protected override void Build(VisualElement group)
    {
        var header = new VisualElement();
        header.AddToClassList("phase");
        title = new Label("-");
        title.AddToClassList("phase__name");
        time = new Label(string.Empty);
        time.AddToClassList("phase__time");
        header.Add(title);
        header.Add(time);
        group.Add(header);

        var track = new VisualElement();
        track.AddToClassList("track");
        fill = new VisualElement();
        fill.AddToClassList("track__fill");
        track.Add(fill);
        group.Add(track);

        var buttons = ButtonRow(group);
        endPhase = Btn(buttons, "페이즈 넘기기", EndPhase);
        nextDay = Btn(buttons, "다음 날", NextDay);

        warning = new Label(string.Empty);
        warning.AddToClassList("warn");
        group.Add(warning);
    }

    public override void Refresh(in DevConsoleState state)
    {
        // 버튼 콜백은 상태를 넘겨받지 못하므로 시계를 여기서 들고 있는다.
        clock = state.ClockUsable ? state.Clock : null;

        if (!state.ClockRunning)
        {
            title.text = "매치 대기";
            time.text = string.Empty;
            fill.style.width = Length.Percent(0f);
            warning.text = string.Empty;
            endPhase.SetEnabled(false);
            nextDay.SetEnabled(false);
            return;
        }

        var phase = state.Clock;
        if (phase.Finished)
        {
            title.text = "판 종료";
            time.text = $"{phase.Day}일차";
            fill.style.width = Length.Percent(100f);
        }
        else
        {
            title.text = $"{phase.Day}일차 · {NameOf(phase.Current)}";
            time.text = $"{Mathf.CeilToInt(phase.Remaining)}초";

            // 경과 비율. Elapsed + Remaining이 그 페이즈의 길이다.
            var total = phase.Elapsed + phase.Remaining;
            var ratio = total > 0.001f ? Mathf.Clamp01(phase.Elapsed / total) : 0f;
            fill.style.width = Length.Percent(ratio * 100f);
        }

        fill.style.backgroundColor = ColorOf(phase.Current);
        warning.text = state.IsServer ? string.Empty : "시계는 서버 권위다. 호스트에서 조작해라.";

        endPhase.SetEnabled(state.IsServer && !phase.Finished);
        nextDay.SetEnabled(state.IsServer && !phase.Finished);
    }

    void EndPhase() { if (clock != null) clock.EndPhaseNowServer(); }

    void NextDay() { if (clock != null) clock.SkipToNextDayServer(); }

    static string NameOf(Phase p) => p switch
    {
        Phase.Night => "밤",
        Phase.Transition => "전환",
        _ => "낮",
    };

    /// 페이즈마다 막대 색이 다르다. 숫자를 읽지 않아도 지금이 밤인지 낮인지 보이게 하려는 것이다.
    /// USS의 플랫 팔레트와 같은 계열로 맞춘다 (밤 #4C7DFF · 전환 #E8A33D · 낮 #E8C93D).
    static Color ColorOf(Phase p) => p switch
    {
        Phase.Night => new Color(0.298f, 0.490f, 1.000f),
        Phase.Transition => new Color(0.910f, 0.639f, 0.239f),
        _ => new Color(0.910f, 0.788f, 0.239f),
    };
}
