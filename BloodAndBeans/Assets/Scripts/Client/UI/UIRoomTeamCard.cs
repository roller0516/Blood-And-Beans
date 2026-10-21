using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// 대기실의 팀 한 칸. 기존 참가자 카드가 초상·이름·준비·팀색을 그린다.
public sealed class UIRoomTeamCard : MonoBehaviour
{
    [SerializeField] TMP_Text heading;
    [SerializeField] UnityEngine.UI.Image teamPlate;
    [SerializeField] UICrewRosterCard[] seats;
    [SerializeField] GameObject selectedFrame;
    [SerializeField] TMP_Text teamNumber;
    [SerializeField] UnityEngine.UI.Image teamAccent;

    public void Render(int team, bool selected, IReadOnlyList<SteamLobby.RoomMember> members)
    {
        heading.text = L10n.Format(TextKey.Room.Team, team + 1);
        teamPlate.color = TeamColors.Of(team);
        if (teamNumber != null) teamNumber.SetText("{0}", team + 1);
        if (teamAccent != null) teamAccent.color = TeamColors.Of(team);
        selectedFrame.SetActive(selected);
        var seat = 0;
        for (var i = 0; i < members.Count && seat < seats.Length; i++)
        {
            var member = members[i];
            if (member.Team != team) continue;
            var icon = CharacterCatalog.IsValid(member.Character)
                ? ResourceManager.Instance.CrewSprite(CharacterCatalog.All[member.Character].Id) : null;
            seats[seat].Render(member.Name, icon, TeamColors.Of(team),
                member.IsReady || member.IsHost, member.IsSelf);
            seats[seat++].SetVisible(true);
        }
        for (; seat < seats.Length; seat++)
        {
            seats[seat].RenderEmpty();
            seats[seat].SetVisible(true);
        }
    }
}
