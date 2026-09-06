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

    /// 방장이 아닌 사람의 버튼. 방장은 시작 버튼을 쥐므로 이건 감춘다 (기획서 10.1).
    [SerializeField] Button readyButton;
    [SerializeField] TMP_Text readyCount;

    [SerializeField] Button leaveButton;

    readonly List<Button> teamButtons = new();
    readonly List<TMP_Text> teamLabels = new();
    readonly List<TMP_Text> spawnedMembers = new();

    Action<int> onSelectTeam;
    int builtTeamCount = -1;

    public void Bind(Action start, Action leave, Action<int> selectTeam, Action toggleReady)
    {
        onSelectTeam = selectTeam;
        UIButtons.Wire(startButton, start);
        UIButtons.Wire(readyButton, toggleReady);
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
                       Func<int, int> occupancyOf, Predicate<int> teamHasRoom,
                       bool selfReady, int readyNow)
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

        // 줄을 지우고 다시 만들지 않고 재사용한다. `Destroy`는 프레임 끝에야 도는데
        // 로비는 스팀 콜백 하나당 한 번씩 다시 그리므로, 한 프레임에 두 번 그리면 아직
        // 지워지지 않은 줄 위에 새 줄이 겹쳐 붙는다 (`SteamLobby.RefreshMembersIfCurrent`).
        if (memberRows != null && memberRowPrefab != null)
        {
            for (var i = 0; i < members.Count; i++)
            {
                if (i >= spawnedMembers.Count) spawnedMembers.Add(Instantiate(memberRowPrefab, memberRows));

                var member = members[i];
                var team = member.Team >= 0 ? $"팀 {member.Team + 1}" : "미정";
                var host = member.IsHost ? " (방장)" : string.Empty;
                var self = member.IsSelf ? " <-" : string.Empty;

                // 방장은 준비 개념이 없으므로 표시하지 않는다. 남은 사람만 준비 여부가 뜬다.
                var ready = member.IsHost ? string.Empty
                          : member.IsReady ? " · 준비" : " · 대기";

                var row = spawnedMembers[i];
                row.gameObject.SetActive(true);
                row.text = $"{member.Name} · {team}{ready}{host}{self}";
            }

            // 나간 사람 자리는 끄기만 한다. 다음에 들어오면 그대로 다시 쓴다.
            for (var i = members.Count; i < spawnedMembers.Count; i++)
                spawnedMembers[i].gameObject.SetActive(false);
        }

        // 방장이 아니면 버튼 자체가 없다. 잠그기만 하면 "왜 안 눌리지"가 되고, 잠금은
        // 방장에게 아직 시작할 수 없는 이유(정원 초과·준비 미완)를 알리는 용도로 남긴다.
        if (startButton != null)
        {
            startButton.gameObject.SetActive(isHost);
            DevHud.SetInteractable(startButton, canStart);
        }

        // 준비 버튼은 방장에게 없다. 시작 버튼과 자리를 나눠 쓰므로 둘이 동시에 뜨지 않는다.
        if (readyButton != null)
        {
            readyButton.gameObject.SetActive(!isHost);
            var label = readyButton.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = selfReady ? "준비 취소" : "준비";
        }

        // 방장은 누구를 기다리는지, 손님은 몇 명이 남았는지 같은 숫자를 본다.
        if (readyCount != null) readyCount.text = $"준비 {readyNow}/{members.Count}";
    }
}
