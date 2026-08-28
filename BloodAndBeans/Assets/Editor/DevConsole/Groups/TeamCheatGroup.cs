using UnityEngine.UIElements;

/// 팀 배정 치트. 런타임 `CheatHud`가 하던 일을 에디터에서도 할 수 있게 옮긴 것이다.
///
/// 팀 명단은 서버 권위라 서버(호스트)에서만 뜻이 있다. 클라이언트에서는 잠긴다 —
/// 클라이언트가 자기 화면에서 눌러 봐야 서버의 배정은 그대로이고, 그걸 모른 채
/// 테스트하면 없는 결함을 쫓게 된다.
public class TeamCheatGroup : DevConsoleGroup
{
    public override string Tab => "치트";
    public override string Title => "팀";

    Label myTeam, teamCount, forcedSeat;
    Button teamCountButton, forcedSeatButton;
    MatchSeating seating;

    protected override void Build(VisualElement group)
    {
        myTeam = Row(group, "내 팀", "-");
        teamCount = Row(group, "팀 수", "-");
        forcedSeat = Row(group, "다음 입장", "-");

        var buttons = ButtonRow(group);
        teamCountButton = Btn(buttons, "팀 수 +1", CycleTeamCount);
        forcedSeatButton = Btn(buttons, "다음 입장 +1", CycleForcedSeat);
    }

    public override void Refresh(in DevConsoleState state)
    {
        // 버튼 콜백은 상태를 넘겨받지 못하므로 좌석 권위를 여기서 들고 있는다.
        seating = state.Seating;

        if (seating == null)
        {
            myTeam.text = "-";
            teamCount.text = "-";
            forcedSeat.text = "-";
            teamCountButton.SetEnabled(false);
            forcedSeatButton.SetEnabled(false);
            return;
        }

        teamCount.text = $"{seating.TeamCount} / {seating.MaxTeams}";
        forcedSeat.text = seating.ForcedSeat < 0 ? "자동" : $"팀 {seating.ForcedSeat}";

        var team = PlayerTeam.Local();
        myTeam.text = !state.Listening ? "-" : team < 0 ? "배정 대기" : $"팀 {team}";

        // 팀 수는 카페가 스폰되기 전에만, 좌석 배정은 서버에서만 바꿀 수 있다.
        teamCountButton.SetEnabled(state.Playing && !state.Listening);
        forcedSeatButton.SetEnabled(state.IsServer);
    }

    /// 1부터 최대치까지 돌아가며 커진다. 카페가 서버 기동 때 한 번만 스폰되므로
    /// 접속 중에는 버튼이 잠겨 있어 여기 오지 않는다.
    void CycleTeamCount()
    {
        if (seating == null) return;
        seating.SetTeamCountCheat(seating.TeamCount >= seating.MaxTeams ? 1 : seating.TeamCount + 1);
    }

    /// 자동(라운드 로빈) → 팀 0 → 팀 1 → … → 자동.
    void CycleForcedSeat()
    {
        if (seating == null) return;
        var next = seating.ForcedSeat + 1;
        seating.SetForcedSeatCheat(next >= seating.TeamCount ? MatchSeating.NoForcedSeat : next);
    }
}
