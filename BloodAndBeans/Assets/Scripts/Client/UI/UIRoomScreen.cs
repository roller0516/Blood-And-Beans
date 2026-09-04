using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 대기실 화면. 팀 선택 줄과 참가자 목록을 보여 준다.
///
/// 팀 버튼 수는 로비가 정하는 팀 수에 따라 달라져서 프리팹에 구워 둘 수 없다. 그래서
/// 팀 버튼과 참가자 줄만 행 프리팹으로 찍어 낸다.
public sealed class UIRoomScreen : UIScreen
{
    [SerializeField] TMP_Text roomTitle;
    [SerializeField] TMP_Text status;
    [SerializeField] RectTransform teamRow;
    [SerializeField] Button teamButtonPrefab;
    [SerializeField] RectTransform memberRows;
    [SerializeField] TMP_Text memberRowPrefab;
    [SerializeField] Button startButton;
    [SerializeField] Button leaveButton;

    readonly List<Button> teamButtons = new();
    readonly List<TMP_Text> teamLabels = new();
    readonly List<GameObject> spawnedMembers = new();

    Action<int> onSelectTeam;
    int builtTeamCount = -1;

    public void Bind(Action start, Action leave, Action<int> selectTeam)
    {
        onSelectTeam = selectTeam;
        UIButtons.Wire(startButton, start);
        UIButtons.Wire(leaveButton, leave);
    }

    /// 팀 수는 로비가 정하고 판마다 다를 수 있다. 같은 수로 다시 들어오면 다시 만들지 않는다.
    public void BuildTeams(int teamCount)
    {
        if (teamCount == builtTeamCount || teamRow == null || teamButtonPrefab == null) return;

        foreach (var button in teamButtons) Destroy(button.gameObject);
        teamButtons.Clear();
        teamLabels.Clear();

        for (var team = 0; team < teamCount; team++)
        {
            var index = team;
            var button = Instantiate(teamButtonPrefab, teamRow);
            button.gameObject.SetActive(true);
            UIButtons.Wire(button, () => onSelectTeam?.Invoke(index));
            teamButtons.Add(button);
            teamLabels.Add(button.GetComponentInChildren<TMP_Text>());
        }
        builtTeamCount = teamCount;
    }

    public void Render(string title, string statusText, IReadOnlyList<SteamLobby.RoomMember> members,
                       int selectedTeam, int playersPerTeam, bool isHost, bool canStart,
                       Func<int, int> occupancyOf, Predicate<int> teamHasRoom)
    {
        if (roomTitle != null) roomTitle.text = title;
        if (status != null) status.text = statusText;

        for (var team = 0; team < teamButtons.Count; team++)
        {
            var mine = team == selectedTeam;
            var marker = mine ? "> " : string.Empty;
            if (teamLabels[team] != null)
                teamLabels[team].text = $"{marker}팀 {team + 1}  {occupancyOf(team)}/{playersPerTeam}";
            DevHud.SetInteractable(teamButtons[team], !mine && teamHasRoom(team));
        }

        foreach (var label in spawnedMembers) Destroy(label);
        spawnedMembers.Clear();

        if (memberRows != null && memberRowPrefab != null)
        {
            foreach (var member in members)
            {
                var team = member.Team >= 0 ? $"팀 {member.Team + 1}" : "미정";
                var host = member.IsHost ? " (방장)" : string.Empty;
                var self = member.IsSelf ? " <-" : string.Empty;

                var row = Instantiate(memberRowPrefab, memberRows);
                row.gameObject.SetActive(true);
                row.text = $"{member.Name} · {team}{host}{self}";
                spawnedMembers.Add(row.gameObject);
            }
        }

        // 방장이 아니면 버튼 자체가 없다. 잠그기만 하면 "왜 안 눌리지"가 되고, 잠금은
        // 방장에게 아직 시작할 수 없는 이유(정원 초과)를 알리는 용도로 남긴다.
        if (startButton != null)
        {
            startButton.gameObject.SetActive(isHost);
            DevHud.SetInteractable(startButton, canStart);
        }
    }
}
