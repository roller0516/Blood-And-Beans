using TMPro;
using UnityEngine;

/// 캐릭터 선택창의 참가자 카드. 로비가 바뀔 때만 실제 명단을 그린다.
public sealed class UICrewRosterCard : MonoBehaviour
{
    [SerializeField] UnityEngine.UI.Image portrait;
    [SerializeField] UnityEngine.UI.Image teamStripe;
    [SerializeField] TMP_Text playerName;
    [SerializeField] GameObject readyMark;
    [SerializeField] GameObject selfMark;
    [SerializeField] TMP_Text readyStatus;
    [SerializeField] UnityEngine.UI.Image readyStatusBackground;
    [SerializeField] Color readyStatusColor = new(.66f, .98f, .42f);
    [SerializeField] Color waitingStatusColor = new(.28f, .17f, .40f, .9f);
    [SerializeField] Color readyTextColor = new(.10f, .18f, .10f);
    [SerializeField] Color waitingTextColor = Color.white;

    public void RenderEmpty()
    {
        var waiting = L10n.Get(TextKey.Select.RosterWaiting);
        Render(waiting, null, UITheme.PanelDeep, false, false);
        if (readyStatus != null) readyStatus.text = waiting;
    }

    public void Render(string nickname, Sprite icon, Color teamColor, bool ready, bool self)
    {
        playerName.text = nickname;
        portrait.sprite = icon;
        portrait.enabled = icon != null;
        teamStripe.color = teamColor;
        readyMark.SetActive(ready);
        selfMark.SetActive(self);
        if (readyStatus != null)
        {
            readyStatus.text = L10n.Get(ready ? TextKey.Select.RosterReady : TextKey.Select.RosterNotReady);
            readyStatus.color = ready ? readyTextColor : waitingTextColor;
        }
        if (readyStatusBackground != null)
            readyStatusBackground.color = ready ? readyStatusColor : waitingStatusColor;
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }
}
