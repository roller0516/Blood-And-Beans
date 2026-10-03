using TMPro;
using UnityEngine;

/// 순위 한 줄 — 원 안의 등수, 팀 이름, 누적 매출 (기획서 3.1). 내 팀만 원을 채워 구분한다.
public sealed class UIStandingRow : MonoBehaviour
{
    [SerializeField] UnityEngine.UI.Image badge;
    [SerializeField] TMP_Text rank;
    [SerializeField] TMP_Text cafe;
    [SerializeField] TMP_Text total;
    /// 시계 HUD 변형에서만 쓴다. 정산 행은 기존 색과 배선을 그대로 사용한다.
    [SerializeField] UnityEngine.UI.Image background;
    [SerializeField] Color[] teamColors = System.Array.Empty<Color>();
    [SerializeField] string ownTeamName;
    [SerializeField] string[] teamNames = System.Array.Empty<string>();
    [SerializeField] Color mineBadge = new(0.36f, 0.78f, 0.66f);
    [SerializeField] Color mineRank = new(0.07f, 0.05f, 0.03f);
    [SerializeField] Color otherBadge = new(1f, 1f, 1f, 0.12f);
    [SerializeField] Color otherRank = new(0.95f, 0.89f, 0.8f, 0.7f);

    public void Render(int place, string name, int revenue, bool mine, int team = -1)
    {
        rank.text = place.ToString();
        if (team >= 0 && team < teamNames.Length && !string.IsNullOrEmpty(teamNames[team])) name = teamNames[team];
        cafe.text = mine && !string.IsNullOrEmpty(ownTeamName) ? ownTeamName : name;
        total.text = revenue.ToString("N0");
        badge.color = mine ? mineBadge : otherBadge;
        rank.color = mine ? mineRank : otherRank;
        if (background != null && team >= 0 && team < teamColors.Length)
        {
            background.color = teamColors[team];
            badge.color = teamColors[team];
        }
    }
}

