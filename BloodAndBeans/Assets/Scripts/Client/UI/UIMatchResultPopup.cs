using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 판이 끝났을 때 뜨는 최종 결산 (기획서 3.1). 마지막 낮이 끝나면 누적 판매 매출이 가장
/// 높은 팀이 승리한다.
///
/// 매출만 보여 준다. 기획서 3.1이 보유 재료·설비·캐릭터는 비공개라고 못박았고, 애초에
/// `Scoreboard`가 복제하는 것도 매출뿐이다.
///
/// **트리는 프리팹에 있다.** 이 클래스는 아무것도 만들지 않고 이어 둔 참조에 값만 넣는다.
/// 순위 칸은 최대 4팀(기획서 10장), 일차 칸은 7일(3.2 임대료 표의 길이)로 깔려 있다.
///
/// `UI_목업.pptx` 8번의 통계 넷(PERFECT RATE · BLOOD BEAN USED · DASH HITS ·
/// FAILED RETURNS)은 넣지 않았다. 기획서 3.1에 없고 집계하는 코드도 없다.
public sealed class UIMatchResultPopup : UIPopup
{
    /// 순위 한 줄.
    [Serializable] public class RankSlot
    {
        public GameObject Root;
        public TMP_Text Rank;
        public TMP_Text Cafe;
        public TMP_Text Revenue;
        public RectTransform Bar;
        public Image BarImage;
        public Image Portrait;
    }

    /// 일차별 매출 한 줄.
    [Serializable] public class DaySlot
    {
        public GameObject Root;
        public TMP_Text Label;
        public TMP_Text Value;
        public RectTransform Bar;
    }

    /// 순위 칸 수는 최대 팀 수와 같다 (기획서 10장: 2/3/4팀).
    public const int RankSlots = 4;

    /// 일차 칸 수. 기획서 3.2 임대료 표가 7일까지다.
    public const int DaySlots = 7;

    [Header("머리")]
    [SerializeField] TMP_Text caption;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text totalText;

    [Header("순위")]
    [SerializeField] RankSlot[] rankSlots = Array.Empty<RankSlot>();
    [SerializeField] string rankFormat = "0";

    [Header("일차별 매출")]
    [SerializeField] DaySlot[] daySlots = Array.Empty<DaySlot>();
    [SerializeField] TMP_Text dailyNote;

    [Header("바닥")]
    [SerializeField] Button lobbyButton;
    [SerializeField] Button rematchButton;

    [Header("색")]
    [SerializeField] Color win = new(0.95f, 0.84f, 0.42f);
    [SerializeField] Color draw = new(0.72f, 0.78f, 0.88f);
    [SerializeField] Color lose = new(0.78f, 0.55f, 0.52f);
    [SerializeField] Color muted = new(0.72f, 0.72f, 0.68f, 0.9f);
    [SerializeField] Color rankText = new(0.16f, 0.085f, 0.035f);
    [SerializeField] Color rankRevenueText = new(1f, 0.94f, 0.75f);
    [SerializeField] Color myRankBar = new(0.24f, 0.42f, 0.18f);
    [SerializeField] Color otherRankBar = new(0.49f, 0.43f, 0.32f);

    /// 판이 끝났으므로 조작할 것이 없다. 커서를 돌려주고 입력을 막는다.
    public override bool BlocksPlayerInput => true;

    /// `revenueByTeam`은 `Scoreboard`가 복제한 값 그대로다. 승패 판정은 여기서 하지 않고
    /// `FinalStandings`에 묻는다 — 씬 없이 기획서와 대조할 수 있어야 하는 규칙이다.
    ///
    /// `lastDay`는 실제로 치른 마지막 일차다. 기획서 3.1은 7일차를 말하지만 판 길이는
    /// `DayPhases.TotalDays`가 정하므로 문구에 숫자를 박지 않는다.
    ///
    /// `dailyRevenue`는 아직 복제되지 않는다. 비워 넘기면 그 칸이 "집계 없음"으로 뜬다.
    public void Bind(int lastDay, IReadOnlyList<int> revenueByTeam, int myTeam,
                     IReadOnlyList<int> dailyRevenue, Action lobby, Action rematch,
                     IReadOnlyList<Sprite> teamPortraits = null)
    {
        var winners = FinalStandings.WinnersOf(revenueByTeam);
        var tie = FinalStandings.IsTie(revenueByTeam);
        var iWon = winners.Contains(myTeam);

        Set(caption, L10n.Get(TextKey.Result.Caption));

        if (tie && iWon)
        {
            Set(titleText, L10n.Get(TextKey.Result.Draw), draw);
        }
        else if (iWon)
        {
            Set(titleText, L10n.Format(TextKey.Result.TeamWin, myTeam + 1), win);
        }
        else
        {
            var first = winners.Count > 0 ? winners[0] : -1;
            Set(titleText, first >= 0 ? L10n.Format(TextKey.Result.TeamWin, first + 1) : L10n.Get(TextKey.Result.MatchEnded), lose);
        }

        var mine = revenueByTeam != null && myTeam >= 0 && myTeam < revenueByTeam.Count
            ? revenueByTeam[myTeam] : 0;
        Set(totalText, $"{mine:N0}", win);

        FillRanks(revenueByTeam, myTeam, teamPortraits);
        FillDays(dailyRevenue);

        UIButtons.Wire(lobbyButton, lobby);
        UIButtons.Wire(rematchButton, rematch);

        // 갈 곳이 없는 버튼은 잠근다. 눌러도 아무 일이 없으면 고장으로 보인다.
        if (lobbyButton != null) lobbyButton.interactable = lobby != null;
        if (rematchButton != null) rematchButton.interactable = rematch != null;
        if (rematchButton != null) rematchButton.gameObject.SetActive(rematch != null);
    }

    void FillRanks(IReadOnlyList<int> revenueByTeam, int myTeam, IReadOnlyList<Sprite> teamPortraits)
    {
        // 매출 내림차순. 같은 값이면 팀 번호 순으로 둔다 — 공동 1위 판정은 이미
        // `FinalStandings`가 했고 여기서는 줄 세우기만 한다.
        var order = new List<int>();
        if (revenueByTeam != null)
            for (var t = 0; t < revenueByTeam.Count; t++) order.Add(t);
        order.Sort((a, b) => revenueByTeam[b].CompareTo(revenueByTeam[a]));

        var top = 1;
        for (var i = 0; i < order.Count; i++) top = Mathf.Max(top, revenueByTeam[order[i]]);

        for (var i = 0; i < rankSlots.Length; i++)
        {
            var slot = rankSlots[i];
            if (slot?.Root == null) continue;

            if (i >= order.Count) { slot.Root.SetActive(false); continue; }
            slot.Root.SetActive(true);

            var team = order[i];
            if (slot.Portrait != null)
            {
                var portrait = teamPortraits != null && team < teamPortraits.Count ? teamPortraits[team] : null;
                slot.Portrait.sprite = portrait;
                slot.Portrait.enabled = portrait != null;
            }
            var value = revenueByTeam[team];
            var tint = team == myTeam ? myRankBar : otherRankBar;

            Set(slot.Rank, (i + 1).ToString(rankFormat), rankText);
            Set(slot.Cafe, DisplayNames.Team(team), rankText);
            Set(slot.Revenue, value.ToString("N0"), rankRevenueText);
            if (slot.BarImage != null) slot.BarImage.color = tint;
            if (slot.Bar != null)
                slot.Bar.localScale = new Vector3(Mathf.Clamp01(value / (float)top), 1f, 1f);
        }
    }

    void FillDays(IReadOnlyList<int> daily)
    {
        var has = daily != null && daily.Count > 0;
        Set(dailyNote, has ? string.Empty : L10n.Get(TextKey.Result.NoDailySales));

        var top = 1;
        if (has) for (var i = 0; i < daily.Count; i++) top = Mathf.Max(top, daily[i]);

        for (var i = 0; i < daySlots.Length; i++)
        {
            var slot = daySlots[i];
            if (slot?.Root == null) continue;

            var known = has && i < daily.Count;
            slot.Root.SetActive(has);
            if (!known) continue;

            Set(slot.Label, $"D{i + 1}");
            Set(slot.Value, daily[i].ToString("N0"));
            if (slot.Bar != null)
                slot.Bar.localScale = new Vector3(Mathf.Clamp01(daily[i] / (float)top), 1f, 1f);
        }
    }

    static void Set(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }

    static void Set(TMP_Text target, string value, Color color)
    {
        if (target == null) return;
        target.text = value;
        target.color = color;
    }
}
