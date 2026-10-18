using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 방 목록의 재사용 행. 화면은 방 데이터와 선택 콜백만 넘긴다.
public sealed class UIRoomListRow : MonoBehaviour
{
    [SerializeField] Button button;
    [SerializeField] TMP_Text roomName;
    [SerializeField] TMP_Text occupancy;
    [SerializeField] GameObject selection;
    [SerializeField] Image thumbnail;
    [SerializeField] Color normalText;
    [SerializeField] Color selectedText;

    public void Bind(LobbyRoom room, bool selected, Action onSelect)
    {
        roomName.text = room.Name;
        occupancy.SetText("{0}/{1}", room.Members, room.Capacity);
        roomName.color = occupancy.color = selected ? selectedText : normalText;
        selection.SetActive(selected);
        UIButtons.Wire(button, onSelect);
    }
}
