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

    readonly List<Button> spawnedRows = new();

    Action<int> onSelectRoom;

    public void Bind(Action refresh, Action create, Action join, Action back, Action<int> selectRoom)
    {
        onSelectRoom = selectRoom;
        UIButtons.Wire(refreshButton, refresh);
        UIButtons.Wire(createButton, create);
        UIButtons.Wire(joinButton, join);
        UIButtons.Wire(backButton, back);
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
