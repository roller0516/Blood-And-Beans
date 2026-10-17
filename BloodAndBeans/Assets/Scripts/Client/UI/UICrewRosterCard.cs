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
    [SerializeField] string waitingLabel = "참가 대기";

    public void RenderEmpty()
    {
        Render(waitingLabel, null, UITheme.PanelDeep, false, false);
    }

    public void Render(string nickname, Sprite icon, Color teamColor, bool ready, bool self)
    {
        playerName.text = nickname;
        portrait.sprite = icon;
        portrait.enabled = icon != null;
        teamStripe.color = teamColor;
        readyMark.SetActive(ready);
        selfMark.SetActive(self);
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }
}
