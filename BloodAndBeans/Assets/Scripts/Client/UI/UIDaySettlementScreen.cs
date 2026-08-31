using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 전환 페이즈(10초)의 정산 화면. 오늘의 매출·임대료, 임대료 납부 결과, 팀 순위,
/// 내일의 손님 예보, 미납 페널티 단계를 한 화면에 놓는다 (기획서 4장 · 3.2 · 3.3 · 5.6).
/// 레이아웃은 `UI_목업.pptx` 6번이다.
///
/// **트리는 프리팹에 있다.** 이 클래스는 아무것도 만들지 않고 이어 둔 참조에 값만 넣는다.
/// 자리·색·글꼴을 바꾸는 것은 프리팹을 열어서 하면 되고 코드를 고칠 필요가 없다.
///
/// 개수가 변하는 목록도 슬롯을 미리 깔아 두고 남는 것은 꺼서 쓴다. 상한이 전부 정해져
/// 있어서 그렇게 할 수 있다 — 순위는 최대 4팀(기획서 10장), 예보는 손님 종족 6종(5.5),
/// 인기 재료는 2~3종(5.6.1), 페널티는 3단계(3.3)다.
///
/// **목업과 기획서가 어긋나는 두 곳은 기획서를 따랐다.** 목업 6번은 일일 목표 금액을 두고
/// 페널티를 낮 효과만으로 그렸는데, 기획서에는 목표 금액 개념이 없고(3.2는 임대료만) 3.3의
/// 페널티는 임대료 미납으로 발동하며 낮·밤 쌍이다. 그래서 목표 자리에는 임대료 납부
/// 결과를, 페널티 칸에는 3.3의 낮·밤 효과를 넣는다.
public sealed class UIDaySettlementScreen : UIScreen
{
    /// 정산 한 줄. 부호와 색은 호출자가 정한다 — 무엇이 이득인지 아는 것은 규칙 쪽이다.
    public readonly struct TradeLine
    {
        public readonly string Label;
        public readonly string Amount;
        public readonly Color Tint;
        public TradeLine(string label, string amount, Color tint)
        {
            Label = label; Amount = amount; Tint = tint;
        }
    }

    /// 순위 한 줄. 목업 6번은 누적과 오늘을 두 개의 막대로 나란히 보여 준다.
    public readonly struct StandingRow
    {
        public readonly string Cafe;
        public readonly int Total;
        public readonly int Today;
        public readonly bool Mine;
        public StandingRow(string cafe, int total, int today, bool mine)
        {
            Cafe = cafe; Total = total; Today = today; Mine = mine;
        }
    }

    /// 예보 카드 한 장. 종족 이름과 인원수는 기획서 5.5 표의 문구를 그대로 받는다.
    public readonly struct GuestCard
    {
        public readonly string Race;
        public readonly int Count;
        public GuestCard(string race, int count) { Race = race; Count = count; }
    }

    public readonly struct PopularItem
    {
        public readonly string Name;
        public readonly int Percent;
        public PopularItem(string name, int percent) { Name = name; Percent = percent; }
    }

    /// 미납 페널티 한 단계. 기획서 3.3 표가 낮·밤 쌍이라 두 줄을 따로 받는다.
    public readonly struct PenaltyStage
    {
        public readonly string Caption;
        public readonly string Day;
        public readonly string Night;
        public PenaltyStage(string caption, string day, string night)
        {
            Caption = caption; Day = day; Night = night;
        }
    }

    /// 프리팹에 깔아 두는 슬롯 수. 프리팹을 만드는 쪽과 채우는 쪽이 같은 값을 봐야 한다.
    public const int TradeSlots = 4;      // 합계 구분선 위에 들어가는 줄 수
    public const int StandingSlots = 4;   // 최대 4팀 (기획서 10장)
    public const int GuestSlots = 6;      // 손님 종족 6종 (기획서 5.5)
    public const int PopularSlots = 3;    // 인기 재료 2~3종 (기획서 5.6.1)
    public const int PenaltySlots = 3;    // 미납 3단계 (기획서 3.3)

    /// 슬롯 한 칸의 부품 묶음. Inspector에서 한 칸이 통째로 접히도록 묶어 둔다.
    [Serializable] public class TradeSlot
    {
        public GameObject Root;
        public TMP_Text Label;
        public TMP_Text Amount;
    }

    [Serializable] public class StandingSlot
    {
        public GameObject Root;
        public TMP_Text Rank;
        public TMP_Text Cafe;
        public TMP_Text Total;
        public TMP_Text Today;
        public RectTransform TotalBar;
        public RectTransform TodayBar;
        public Image TotalBarImage;
    }

    [Serializable] public class GuestSlot
    {
        public GameObject Root;
        public TMP_Text Race;
        public TMP_Text Count;
    }

    [Serializable] public class PopularSlot
    {
        public GameObject Root;
        public TMP_Text Name;
        public TMP_Text Bonus;
    }

    [Serializable] public class PenaltySlot
    {
        public Image Background;
        public TMP_Text Caption;
        public TMP_Text Day;
        public TMP_Text Night;
    }

    [Header("머리")]
    [SerializeField] TMP_Text dayHeading;
    [SerializeField] TMP_Text countdown;
    [SerializeField] RectTransform countdownFill;
    [SerializeField] TMP_Text rentCaption;
    [SerializeField] TMP_Text rentPaidText;
    [SerializeField] TMP_Text rentOwedText;
    [SerializeField] Image rentBadge;
    [SerializeField] TMP_Text rentBadgeLabel;

    [Header("오늘의 거래")]
    [SerializeField] TMP_Text todaySales;
    [SerializeField] TMP_Text rentDue;
    [SerializeField] TMP_Text debtCarried;
    [SerializeField] TMP_Text debtNote;
    [SerializeField] TradeSlot[] tradeSlots = Array.Empty<TradeSlot>();

    [Header("순위")]
    [SerializeField] StandingSlot[] standingSlots = Array.Empty<StandingSlot>();

    [Header("예보")]
    [SerializeField] GuestSlot[] guestSlots = Array.Empty<GuestSlot>();
    [SerializeField] PopularSlot[] popularSlots = Array.Empty<PopularSlot>();

    [Header("페널티")]
    [SerializeField] TMP_Text penaltyState;
    [SerializeField] PenaltySlot[] penaltySlots = Array.Empty<PenaltySlot>();

    /// 전환 페이즈가 시작될 때 한 번 부른다. 남은 시간만 계속 바뀌므로 `SetRemaining`으로
    /// 따로 준다.
    ///
    /// `missStreak`은 누적 미납 횟수다 (`Rent.MissStreak`). 0이면 완납이라 페널티가 없다.
    public void Bind(int day,
                     IReadOnlyList<TradeLine> lines, int sales,
                     int rentOwed, int rentPaid, int debt, int tomorrowRent,
                     IReadOnlyList<StandingRow> standings,
                     IReadOnlyList<GuestCard> guests,
                     IReadOnlyList<PopularItem> popular,
                     int missStreak, IReadOnlyList<PenaltyStage> penalties)
    {
        Set(dayHeading, $"DAY {day:00} 정산");
        Set(todaySales, sales.ToString("N0"));
        Set(rentDue, rentOwed > 0 ? $"−{rentOwed:N0}" : "0");
        Set(debtCarried, debt.ToString("N0"));
        Set(debtNote, $"이자 없음 · 내일 임대료 {tomorrowRent:N0}");

        BindRent(day, rentOwed, rentPaid, missStreak);
        FillTrade(lines);
        FillStandings(standings);
        FillGuests(guests);
        FillPopular(popular);
        FillPenalties(missStreak, penalties);
    }

    /// 남은 시간. 전환은 10초라 초 단위로 충분하다 (기획서 4장).
    public void SetRemaining(float seconds, float total)
    {
        Set(countdown, Mathf.CeilToInt(Mathf.Max(seconds, 0f)).ToString());
        if (countdownFill != null)
            countdownFill.localScale =
                new Vector3(total > 0f ? Mathf.Clamp01(seconds / total) : 0f, 1f, 1f);
    }

    /// 임대료 납부 결과 (기획서 3.2). 부족분은 부채로 이월되므로 완납 여부는 실제로 낸
    /// 금액과 청구액을 비교해 판정한다.
    void BindRent(int day, int owed, int paid, int missStreak)
    {
        Set(rentCaption, $"DAY {day:00} RENT");
        Set(rentPaidText, paid.ToString("N0"));
        Set(rentOwedText, $"/ {owed:N0}");

        var settled = paid >= owed;
        if (rentPaidText != null) rentPaidText.color = settled ? UITheme.Green : UITheme.Red;
        if (rentBadge != null) rentBadge.color = settled ? UITheme.Green : UITheme.Red;
        Set(rentBadgeLabel, settled ? "완납" : "미납");

        Set(penaltyState, settled
            ? "완납 — 페널티 적용 받지 않음"
            : $"미납 {missStreak}회 — {Mathf.Clamp(missStreak, 1, PenaltySlots)}단계 적용");
        if (penaltyState != null)
            penaltyState.color = settled ? UITheme.Green : UITheme.Red;
    }

    void FillTrade(IReadOnlyList<TradeLine> lines)
    {
        var count = lines != null ? lines.Count : 0;

        // 넘치면 마지막 줄을 요약으로 바꾼다. 조용히 버리면 오늘 매출의 근거가 사라지는데
        // 합계는 그대로라 플레이어가 차이를 확인할 방법이 없어진다.
        var overflow = count > tradeSlots.Length;
        var shown = overflow ? tradeSlots.Length - 1 : count;

        for (var i = 0; i < tradeSlots.Length; i++)
        {
            var slot = tradeSlots[i];
            if (slot?.Root == null) continue;

            if (i < shown)
            {
                slot.Root.SetActive(true);
                Set(slot.Label, lines[i].Label);
                Set(slot.Amount, lines[i].Amount);
                if (slot.Amount != null) slot.Amount.color = lines[i].Tint;
            }
            else if (overflow && i == shown)
            {
                slot.Root.SetActive(true);
                Set(slot.Label, $"그 외 {count - shown}건");
                Set(slot.Amount, "…");
                if (slot.Amount != null) slot.Amount.color = UITheme.Cream;
            }
            else slot.Root.SetActive(false);
        }
    }

    void FillStandings(IReadOnlyList<StandingRow> rows)
    {
        var count = rows != null ? rows.Count : 0;
        var topTotal = 1;
        var topToday = 1;
        for (var i = 0; i < count; i++)
        {
            topTotal = Mathf.Max(topTotal, rows[i].Total);
            topToday = Mathf.Max(topToday, rows[i].Today);
        }

        for (var i = 0; i < standingSlots.Length; i++)
        {
            var slot = standingSlots[i];
            if (slot?.Root == null) continue;

            if (i >= count) { slot.Root.SetActive(false); continue; }
            slot.Root.SetActive(true);

            var row = rows[i];
            var tint = row.Mine ? UITheme.GoldLit : UITheme.Cream;

            Set(slot.Rank, $"{i + 1:00}");
            Set(slot.Cafe, row.Cafe);
            Set(slot.Total, row.Total.ToString("N0"));
            Set(slot.Today, row.Today.ToString("N0"));
            if (slot.Total != null) slot.Total.color = tint;
            if (slot.TotalBarImage != null) slot.TotalBarImage.color = tint;

            if (slot.TotalBar != null)
                slot.TotalBar.localScale =
                    new Vector3(Mathf.Clamp01(row.Total / (float)topTotal), 1f, 1f);
            if (slot.TodayBar != null)
                slot.TodayBar.localScale =
                    new Vector3(Mathf.Clamp01(row.Today / (float)topToday), 1f, 1f);
        }
    }

    void FillGuests(IReadOnlyList<GuestCard> guests)
    {
        for (var i = 0; i < guestSlots.Length; i++)
        {
            var slot = guestSlots[i];
            if (slot?.Root == null) continue;

            var has = guests != null && i < guests.Count;
            Set(slot.Race, has && !string.IsNullOrEmpty(guests[i].Race) ? guests[i].Race : "—");
            Set(slot.Count, $"×{(has ? guests[i].Count : 0)}");
        }
    }

    void FillPopular(IReadOnlyList<PopularItem> popular)
    {
        var count = popular != null ? popular.Count : 0;
        for (var i = 0; i < popularSlots.Length; i++)
        {
            var slot = popularSlots[i];
            if (slot?.Root == null) continue;

            if (i >= count) { slot.Root.SetActive(false); continue; }
            slot.Root.SetActive(true);
            Set(slot.Name, popular[i].Name);
            Set(slot.Bonus, $"+{popular[i].Percent}%");
        }
    }

    void FillPenalties(int missStreak, IReadOnlyList<PenaltyStage> penalties)
    {
        // 지금 걸린 단계만 밝게 둔다. 나머지는 "다음에 이렇게 된다"는 예고다.
        var active = Mathf.Clamp(missStreak, 0, PenaltySlots);

        for (var i = 0; i < penaltySlots.Length; i++)
        {
            var slot = penaltySlots[i];
            if (slot == null) continue;

            var has = penalties != null && i < penalties.Count;
            Set(slot.Caption, has && !string.IsNullOrEmpty(penalties[i].Caption)
                ? penalties[i].Caption : $"{i + 1}회 연속");

            // 어느 쪽이 낮이고 밤인지는 색만으로 갈리지 않는다. 머리글자를 붙인다.
            Set(slot.Day, has && !string.IsNullOrEmpty(penalties[i].Day)
                ? "낮 · " + penalties[i].Day : string.Empty);
            Set(slot.Night, has && !string.IsNullOrEmpty(penalties[i].Night)
                ? "밤 · " + penalties[i].Night : string.Empty);

            var on = active > 0 && i == active - 1;
            if (slot.Background != null)
                slot.Background.color = on ? UITheme.Panel : UITheme.PanelDeep;
            if (slot.Caption != null) slot.Caption.color = on ? UITheme.Red : UITheme.Gold;
            if (slot.Day != null) slot.Day.color = on ? UITheme.Cream : UITheme.Cream * 0.55f;
            if (slot.Night != null) slot.Night.color = on ? UITheme.Blue : UITheme.Blue * 0.55f;
        }
    }

    /// 참조가 비어 있어도 터지지 않게 한 곳에서 막는다. 프리팹에서 한 칸을 지우는 것은
    /// 기획자가 할 수 있는 일이고, 그때 화면 전체가 멈추면 안 된다.
    static void Set(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }
}
