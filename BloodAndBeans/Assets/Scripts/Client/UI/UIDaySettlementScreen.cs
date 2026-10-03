using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 전환 페이즈(10초)의 정산 화면. 기획서 4.1 표의 네 칸 — 하루 정산 · 순위 ·
/// 자동 업그레이드 · 내일의 손님 예보 — 을 2×2 카드로 놓는다.
///
/// **트리는 프리팹에 있다.** 이 클래스는 아무것도 만들지 않고 이어 둔 파츠에 값만 넣는다.
/// 상한이 기획서로 정해진 목록이라 칸을 미리 깔아 두고 남는 것은 끈다 — 순위는 최대
/// 4팀(10장), 보석은 6종(8.2), 인기 재료는 2~3종(5.6.1), 손님 종족은 6종(5.5)이다.
public sealed class UIDaySettlementScreen : UIScreen
{
    /// 순위 한 줄. 정렬은 호출자가 한다.
    public readonly struct StandingRow
    {
        public readonly string Cafe;
        public readonly int Total;
        public readonly bool Mine;
        public StandingRow(string cafe, int total, bool mine)
        {
            Cafe = cafe; Total = total; Mine = mine;
        }
    }

    /// 적용 중인 보석 한 줄 (기획서 4.1 「자동 업그레이드」). 턴 수는 다음 낮 기준이다.
    public readonly struct GemRow
    {
        public readonly Sprite Icon;
        public readonly string Name;
        public readonly string Effect;
        public readonly int Turns;
        public readonly bool Refreshed;
        public GemRow(Sprite icon, string name, string effect, int turns, bool refreshed)
        {
            Icon = icon; Name = name; Effect = effect; Turns = turns; Refreshed = refreshed;
        }
    }

    [Header("머리")]
    [SerializeField] TMP_Text dayHeading;
    [SerializeField] TMP_Text countdown;
    [SerializeField] RectTransform countdownFill;

    [Header("하루 정산")]
    [SerializeField] UISettlementLine salesLine;
    [SerializeField] UISettlementLine rentLine;
    [SerializeField] UISettlementLine paymentLine;
    [SerializeField] UISettlementLine streakLine;

    [Header("순위")]
    [SerializeField] UIStandingRow[] standingRows = Array.Empty<UIStandingRow>();

    [Header("자동 업그레이드")]
    [SerializeField] TMP_Text gemNote;
    [SerializeField] UIGemRow[] gemRows = Array.Empty<UIGemRow>();

    [Header("예보")]
    /// `Race` 순서로 잇는다. 칸 색과 놓인 순서는 프리팹이 정한다.
    [SerializeField] LayoutElement[] raceSegments = Array.Empty<LayoutElement>();
    [SerializeField] UIDayItemIcon[] popularIcons = Array.Empty<UIDayItemIcon>();

    /// 전환 페이즈가 시작될 때 한 번 부른다. 남은 시간만 계속 바뀌므로 `SetRemaining`으로
    /// 따로 준다. `missStreak`은 연속 미납 횟수다 (`Rent.MissStreak`).
    public void Bind(int day, int sales, int rentOwed, int rentPaid, int missStreak,
                     IReadOnlyList<StandingRow> standings,
                     IReadOnlyList<GemRow> gems,
                     IReadOnlyList<int> raceCounts,
                     IReadOnlyList<Sprite> popular)
    {
        dayHeading.text = $"DAY {day:00} 정산";

        // 부족분은 부채로 이월되므로 완납 여부는 낸 금액과 청구액으로 판정한다 (기획서 3.2).
        var settled = rentPaid >= rentOwed;
        salesLine.Render(sales.ToString("N0"), UITheme.Cream);
        rentLine.Render(rentOwed.ToString("N0"), UITheme.Cream);
        paymentLine.Render(settled ? "완납" : "미납", settled ? UITheme.Green : UITheme.Red);
        streakLine.Render($"{missStreak}일", missStreak > 0 ? UITheme.Red : UITheme.Green);

        FillStandings(standings);
        FillGems(gems);
        FillForecast(raceCounts, popular);
    }

    /// 남은 시간. 전환은 10초라 초 단위로 충분하다 (기획서 4장).
    public void SetRemaining(float seconds, float total)
    {
        countdown.text = Mathf.CeilToInt(Mathf.Max(seconds, 0f)).ToString();
        countdownFill.localScale =
            new Vector3(total > 0f ? Mathf.Clamp01(seconds / total) : 0f, 1f, 1f);
    }

    void FillStandings(IReadOnlyList<StandingRow> rows)
    {
        var count = rows != null ? rows.Count : 0;
        for (var i = 0; i < standingRows.Length; i++)
        {
            var shown = i < count;
            standingRows[i].gameObject.SetActive(shown);
            if (shown) standingRows[i].Render(i + 1, rows[i].Cafe, rows[i].Total, rows[i].Mine);
        }
    }

    /// 귀환에 성공해 이번 밤에 들어온 보석까지 포함한다. 고르는 절차는 없다 (기획서 8.1).
    void FillGems(IReadOnlyList<GemRow> gems)
    {
        var count = gems != null ? gems.Count : 0;
        gemNote.gameObject.SetActive(count == 0);

        for (var i = 0; i < gemRows.Length; i++)
        {
            var shown = i < count;
            gemRows[i].gameObject.SetActive(shown);
            if (shown) gemRows[i].Render(gems[i].Icon, gems[i].Turns, gems[i].Name, gems[i].Effect, gems[i].Refreshed);
        }
    }

    /// 수가 아니라 구성비다 (기획서 5.6) — 칸 폭만 인원수에 비례하고 숫자는 적지 않는다.
    void FillForecast(IReadOnlyList<int> raceCounts, IReadOnlyList<Sprite> popular)
    {
        for (var i = 0; i < raceSegments.Length; i++)
        {
            var n = raceCounts != null && i < raceCounts.Count ? raceCounts[i] : 0;
            raceSegments[i].gameObject.SetActive(n > 0);
            raceSegments[i].flexibleWidth = n;
        }

        var count = popular != null ? popular.Count : 0;
        for (var i = 0; i < popularIcons.Length; i++)
        {
            var shown = i < count;
            popularIcons[i].gameObject.SetActive(shown);
            if (shown) popularIcons[i].Render(popular[i], null, true, true, false);
        }
    }
}
