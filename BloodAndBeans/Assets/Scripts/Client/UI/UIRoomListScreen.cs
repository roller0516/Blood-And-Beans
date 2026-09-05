using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 방 목록 화면. 목록 줄은 방 개수만큼 런타임에 만들어야 해서 행 프리팹을 하나 들고 있다.
public sealed class UIRoomListScreen : UIScreen
{
    [SerializeField] TMP_Text status;
    [SerializeField] RectTransform rows;
    [SerializeField] Button rowPrefab;
    [SerializeField] Button refreshButton;
    [SerializeField] Button createButton;
    [SerializeField] Button joinButton;
    [SerializeField] Button backButton;

    /// 화면에 한 번에 그리는 방의 최대 수. 목록 자체의 상한은 SteamLobby가 가진다.
    [SerializeField, Min(1)] int maxRows = 8;

    [Header("팀 수 (기획서 10장: 2/3/4팀)")]
    [SerializeField] TMP_Text teamCountLabel;
    [SerializeField] Button teamCountMinus;
    [SerializeField] Button teamCountPlus;

    /// 하한은 여기서 고정한다 — 1팀은 경쟁이 성립하지 않는다. 상한은 `SteamLobby.MaxTeams`
    /// 에서 읽는다(`Bind`) — 그 값이 진실의 원천이라 여기 다시 박지 않는다.
    const int MinTeams = 2;
    int maxTeamsUi = 4;

    /// 방 만들기를 누른 순간의 팀 수. 기본값은 `SteamLobby.MaxTeams`와 같다 — 기존
    /// 동작(항상 4팀)을 그대로 유지한 채 선택지를 여는 것이라, 아무도 안 건드리면
    /// 예전과 같은 방이 만들어진다.
    public int SelectedTeamCount { get; private set; }

    readonly List<Button> spawnedRows = new();

    Action<int> onSelectRoom;
    Action<int> onCreate;

    public void Bind(Action refresh, Action<int> create, Action join, Action back,
                     Action<int> selectRoom, int maxTeams)
    {
        onSelectRoom = selectRoom;
        onCreate = create;
        maxTeamsUi = Mathf.Max(MinTeams, maxTeams);
        SelectedTeamCount = maxTeamsUi;

        UIButtons.Wire(refreshButton, refresh);
        UIButtons.Wire(createButton, () => onCreate?.Invoke(SelectedTeamCount));
        UIButtons.Wire(joinButton, join);
        UIButtons.Wire(backButton, back);
        UIButtons.Wire(teamCountMinus, () => StepTeamCount(-1));
        UIButtons.Wire(teamCountPlus, () => StepTeamCount(1));

        RefreshTeamCountLabel();
    }

    void StepTeamCount(int delta)
    {
        SelectedTeamCount = Mathf.Clamp(SelectedTeamCount + delta, MinTeams, maxTeamsUi);
        RefreshTeamCountLabel();
    }

    void RefreshTeamCountLabel()
    {
        if (teamCountLabel != null) teamCountLabel.text = $"{SelectedTeamCount}팀";
        DevHud.SetInteractable(teamCountMinus, SelectedTeamCount > MinTeams);
        DevHud.SetInteractable(teamCountPlus, SelectedTeamCount < maxTeamsUi);
    }

    public void Render(string statusText, IReadOnlyList<SteamLobby.RoomInfo> rooms, int selectedRoom)
    {
        if (status != null) status.text = statusText;

        foreach (var row in spawnedRows) Destroy(row.gameObject);
        spawnedRows.Clear();

        if (rows == null || rowPrefab == null) return;

        var count = Mathf.Min(rooms.Count, maxRows);
        for (var i = 0; i < count; i++)
        {
            var index = i;
            var room = rooms[i];
            var row = Instantiate(rowPrefab, rows);
            row.gameObject.SetActive(true);

            var label = row.GetComponentInChildren<TMP_Text>();
            if (label != null) label.text = $"{room.Name}  {room.Members}/{room.Capacity}";

            UIButtons.Wire(row, () => onSelectRoom?.Invoke(index));
            DevHud.SetInteractable(row, index != selectedRoom);
            spawnedRows.Add(row);
        }

        DevHud.SetInteractable(joinButton, selectedRoom >= 0);
    }
}
