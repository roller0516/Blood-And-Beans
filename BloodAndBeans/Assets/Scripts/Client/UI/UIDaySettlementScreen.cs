using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 전환 페이즈(10초)의 정산 화면. 오늘의 매출·임대료, 임대료 납부 결과, 팀 순위,
/// 내일의 손님 예보, 미납 페널티 단계를 한 화면에 놓는다 (기획서 4장 · 3.2 · 3.3 · 5.6).
/// 레이아웃은 `UI_목업.pptx` 6번이다.
///
/// **목업과 기획서가 어긋나는 두 곳은 기획서를 따랐다.** 목업 6번은 일일 목표 금액
/// (`DAY 04 TARGET 775 / 700`)을 두고 페널티를 `제작 속도 −10% / 그릇 1개 압류 /
/// 손님 대기 −1칸`으로 그렸는데, 기획서에는 목표 금액 개념이 없고(3.2는 임대료만) 3.3의
/// 페널티는 임대료 미납으로 발동하며 낮·밤 쌍이다. 그래서 목표 자리에는 임대료 납부
/// 결과를, 페널티 칸에는 3.3의 낮·밤 효과를 넣는다. 목업의 자리와 크기는 그대로 쓴다.
///
/// 이 화면은 값을 만들지 않는다. 얼마를 벌었고 임대료가 얼마인지는 `TeamLedger`·`Rent`가,
/// 내일 손님은 `Forecast`가 정한다. 여기는 받은 값을 정해진 자리에 그리기만 한다.
/// 페널티 문구도 인자로 받는다 — 3.3 표의 내용을 화면이 들고 있으면 규칙이 두 곳에 산다.
///
/// 위젯을 프리팹이 아니라 코드로 세우는 이유는 `MatchHudScreen`과 같다 — 행이 데이터
/// 개수만큼 늘어나고 자리가 서로 물려 있어 Inspector로 흩어 두면 유지되지 않는다.
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
    /// 빈 칸은 이름을 비우고 `Count`를 0으로 넘기면 목업처럼 `—  ×0`으로 그려진다.
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
    /// 문구는 규칙 쪽에서 넘긴다 — 표의 내용을 화면이 들고 있으면 규칙이 두 곳에 산다.
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

    /// 목업 6번의 가로 폭이 이 개수에 맞춰져 있다. 넘치면 자리가 아니라 목업이 바뀌어야 한다.
    /// 인기 재료 3칸은 기획서 5.6.1의 2~3종과 같다.
    const int GuestSlots = 8;
    const int PopularSlots = 3;
    const int PenaltySlots = 3;

    RectTransform stage;
    TMP_Text dayHeading, countdown, targetValue, targetGoal, targetBadge;
    TMP_Text todaySales, rentDue, debtCarried, debtNote, penaltyState;
    RectTransform countdownFill, targetBadgeBox;
    Transform tradePanel, standingPanel, forecastPanel, penaltyPanel;

    readonly List<GameObject> tradeRows = new();
    readonly List<GameObject> standingRows = new();
    readonly List<GameObject> guestCards = new();
    readonly List<GameObject> popularChips = new();
    readonly List<TMP_Text> penaltyCaptions = new();
    readonly List<TMP_Text> penaltyDays = new();
    readonly List<TMP_Text> penaltyNights = new();
    readonly List<UnityEngine.UI.Image> penaltySlots = new();

    bool built;

    protected override void Awake()
    {
        base.Awake();
        Build();
    }

    void Build()
    {
        if (built) return;
        built = true;

        stage = UITheme.Stage(transform, UITheme.Ink);

        UITheme.Caption(stage, "INTERMISSION", 48f, 71f, 187f);
        dayHeading = UITheme.Text(stage, "DayHeading", "DAY 00 정산", 32f,
                                  UITheme.Cream, 238f, 51f, 400f, 50f);

        BuildTargetBadge();

        // 전환 10초의 잔여 시간. 막대와 숫자가 같은 값을 본다.
        UITheme.Box(stage, "CountdownTrack", UITheme.Cream, 1550f, 72f, 280f, 6f);
        countdownFill = UITheme.Bar(stage, UITheme.Cream, UITheme.Gold, 1550f, 72f, 280f, 6f);
        countdown = UITheme.Text(stage, "Countdown", "0", 33f, UITheme.Red,
                                 1830f, 53f, 50f, 48f, TextAlignmentOptions.TopRight);

        UITheme.Rule(stage, UITheme.Gold, 48f, 115f, 1824f);

        BuildTradePanel();
        BuildStandingPanel();
        BuildForecastPanel();
        BuildPenaltyPanel();
    }

    /// 목업 6번 상단의 목표 자리. 기획서에 목표 금액이 없어(3.2는 임대료만) 그 자리에
    /// 임대료 납부 결과를 넣는다 — 납부액 / 청구액 과 완납·미납 배지.
    void BuildTargetBadge()
    {
        targetGoal = UITheme.Text(stage, "RentCaption", "DAY 00 RENT", 8f, UITheme.Gold,
                                  1140f, 36f, 178f, 20f, TextAlignmentOptions.TopRight);
        targetValue = UITheme.Text(stage, "RentPaid", "0", 22f, UITheme.Green,
                                   1100f, 52f, 171f, 49f, TextAlignmentOptions.TopRight);
        targetGoalText = UITheme.Text(stage, "RentOwed", "/ 0", 14f, UITheme.Cream,
                                      1271f, 67f, 47f, 30f);

        targetBadgeBox = UITheme.Box(stage, "RentBadge", UITheme.Green, 1344f, 40f, 181f, 57f);
        targetBadge = UITheme.Text(targetBadgeBox, "Label", "완납", 20f, UITheme.Ink,
                                   0f, 12f, 181f, 43f, TextAlignmentOptions.Top);
    }

    TMP_Text targetGoalText;

    void BuildTradePanel()
    {
        tradePanel = UITheme.Box(stage, "TodaysTrade", UITheme.Panel, 48f, 140f, 472f, 916f);
        UITheme.Caption(tradePanel, "TODAY'S TRADE", 23f, 15f, 440f);
        UITheme.Rule(tradePanel, UITheme.Gold, 1f, 46f, 470f);

        UITheme.Rule(tradePanel, UITheme.Gold, 23f, 235f, 426f);

        UITheme.Text(tradePanel, "SalesLabel", "오늘 매출", 16f, UITheme.Cream,
                     23f, 270f, 103f, 32f);
        todaySales = UITheme.Text(tradePanel, "SalesValue", "0", 24f, UITheme.GoldLit,
                                  260f, 253f, 197f, 51f, TextAlignmentOptions.TopRight);

        UITheme.Text(tradePanel, "RentLabel", "임대료", 16f, UITheme.Cream,
                     23f, 330f, 75f, 32f);
        rentDue = UITheme.Text(tradePanel, "RentValue", "0", 24f, UITheme.Red,
                               260f, 313f, 197f, 51f, TextAlignmentOptions.TopRight);

        UITheme.Rule(tradePanel, UITheme.Gold, 1f, 819f, 470f);
        UITheme.Text(tradePanel, "DebtCaption", "DEBT CARRIED", 8f, UITheme.Red,
                     23f, 864f, 176f, 20f);
        debtNote = UITheme.Text(tradePanel, "DebtNote", string.Empty, 10f, UITheme.Cream,
                                23f, 883f, 260f, 20f);
        debtCarried = UITheme.Text(tradePanel, "DebtValue", "0", 26f, UITheme.Cream,
                                   300f, 836f, 157f, 55f, TextAlignmentOptions.TopRight);
    }

    void BuildStandingPanel()
    {
        standingPanel = UITheme.Box(stage, "Standings", UITheme.Panel, 544f, 140f, 412f, 916f);
        UITheme.Caption(standingPanel, "STANDINGS", 23f, 15f, 378f);
        UITheme.Rule(standingPanel, UITheme.Gold, 1f, 46f, 410f);

        // 범례. 두 막대가 무엇인지 알려 주지 않으면 누적과 오늘을 구분할 수 없다.
        UITheme.Box(standingPanel, "LegendTotalSwatch", UITheme.Gold, 23f, 68f, 14f, 5f);
        UITheme.Text(standingPanel, "LegendTotal", "누적", 8f, UITheme.Cream, 43f, 63f, 32f, 19f);
        UITheme.Box(standingPanel, "LegendTodaySwatch", UITheme.Blue, 83f, 68f, 14f, 5f);
        UITheme.Text(standingPanel, "LegendToday", "오늘", 8f, UITheme.Cream, 103f, 63f, 32f, 19f);

        UITheme.Rule(standingPanel, UITheme.Gold, 1f, 860f, 410f);
        UITheme.Text(standingPanel, "Note",
                     "공개되는 것은 매출뿐. 보유 재료 · 설비 · 캐릭터는 비공개.",
                     10f, UITheme.Cream, 23f, 877f, 378f, 26f);
    }

    void BuildForecastPanel()
    {
        forecastPanel = UITheme.Box(stage, "Forecast", UITheme.Panel, 980f, 140f, 892f, 766f);
        UITheme.Caption(forecastPanel, "TOMORROW'S GUESTS", 25f, 15f, 208f);
        UITheme.Text(forecastPanel, "Blind", "어느 박스에 그 재료가 들었는지는 알려주지 않는다",
                     9f, UITheme.Cream, 598f, 18f, 295f, 19f);
        UITheme.Rule(forecastPanel, UITheme.Gold, 1f, 47f, 890f);

        // 카드 사이의 금색 틈. 카드 배경을 이 위에 얹어서 1px 구분선처럼 보이게 한다.
        UITheme.Box(forecastPanel, "GuestStrip", UITheme.Gold, 1f, 48f, 890f, 148f);

        UITheme.Rule(forecastPanel, UITheme.Gold, 1f, 196f, 890f);
        UITheme.Text(forecastPanel, "HotCaption", "HOT INGREDIENTS", 9f, UITheme.Green,
                     25f, 231f, 160f, 21f);
    }

    /// 목업 6번 하단의 페널티 칸. 기획서 3.3은 누적 미납 단계마다 낮·밤 효과가 쌍으로
    /// 붙으므로 한 칸에 두 줄을 넣는다. 지금 걸린 단계만 밝게 둔다.
    void BuildPenaltyPanel()
    {
        penaltyPanel = UITheme.Box(stage, "Penalty", UITheme.Panel, 980f, 924f, 892f, 132f);
        UITheme.Caption(penaltyPanel, "PENALTY", 25f, 19f, 83f);
        penaltyState = UITheme.Text(penaltyPanel, "State", string.Empty, 10f, UITheme.Green,
                                    116f, 22f, 420f, 20f);
        UITheme.Text(penaltyPanel, "Escalation", "연속 미납 시 단계가 강화된다", 9f,
                     UITheme.Cream, 452f, 22f, 415f, 19f, TextAlignmentOptions.TopRight);

        for (var i = 0; i < PenaltySlots; i++)
        {
            // 목업은 한 줄짜리 칸이지만 기획서 3.3은 낮·밤이 쌍이라 두 줄이 필요하다.
            // 목업에 없는 배치라 칸을 키웠다 — 아래 예보 패널이 y=906에서 끝나므로 자리는 남는다.
            var slot = UITheme.Box(penaltyPanel, $"Stage{i}", UITheme.PanelDeep,
                                   25f + i * 285f, 50f, 273f, 74f);
            penaltySlots.Add(slot.GetComponent<UnityEngine.UI.Image>());

            penaltyCaptions.Add(UITheme.Text(slot, "Caption", $"{i + 1}회 연속", 8f,
                                             UITheme.Gold, 14f, 8f, 245f, 18f));
            penaltyDays.Add(UITheme.Text(slot, "Day", string.Empty, 10f,
                                         UITheme.Cream, 14f, 27f, 245f, 20f));
            penaltyNights.Add(UITheme.Text(slot, "Night", string.Empty, 10f,
                                           UITheme.Blue, 14f, 48f, 245f, 20f));
        }
    }

    /// 전환 페이즈가 시작될 때 한 번 부른다. 남은 시간만 계속 바뀌므로 `SetRemaining`으로
    /// 따로 준다 — 여기서 매 프레임 행을 다시 만들면 GC가 붙는다.
    /// `missStreak`은 누적 미납 횟수다 (`Rent.MissStreak`). 0이면 완납이라 페널티가 없다.
    /// 단계는 3에서 멈춘다 — 기획서 3.3에 4회 행이 없다.
    public void Bind(int day,
                     IReadOnlyList<TradeLine> lines, int sales,
                     int rentOwed, int rentPaid, int debt, int tomorrowRent,
                     IReadOnlyList<StandingRow> standings,
                     IReadOnlyList<GuestCard> guests,
                     IReadOnlyList<PopularItem> popular,
                     int missStreak, IReadOnlyList<PenaltyStage> penalties)
    {
        Build();

        dayHeading.text = $"DAY {day:00} 정산";
        todaySales.text = sales.ToString("N0");
        rentDue.text = rentOwed > 0 ? $"−{rentOwed:N0}" : "0";
        debtCarried.text = debt.ToString("N0");
        debtNote.text = $"이자 없음 · 내일 임대료 {tomorrowRent:N0}";

        BindRent(day, rentOwed, rentPaid, missStreak);

        // 지금 걸린 단계만 밝게 둔다. 나머지는 "다음에 이렇게 된다"는 예고다.
        var active = Mathf.Clamp(missStreak, 0, PenaltySlots);
        for (var i = 0; i < PenaltySlots; i++)
        {
            var has = penalties != null && i < penalties.Count;
            penaltyCaptions[i].text = has && !string.IsNullOrEmpty(penalties[i].Caption)
                ? penalties[i].Caption : $"{i + 1}회 연속";
            // 어느 쪽이 낮이고 밤인지는 색만으로 갈리지 않는다. 표시용 머리글자를 붙인다.
            penaltyDays[i].text = has && !string.IsNullOrEmpty(penalties[i].Day)
                ? "낮 · " + penalties[i].Day : string.Empty;
            penaltyNights[i].text = has && !string.IsNullOrEmpty(penalties[i].Night)
                ? "밤 · " + penalties[i].Night : string.Empty;

            var on = active > 0 && i == active - 1;
            penaltySlots[i].color = on ? UITheme.Panel : UITheme.PanelDeep;
            penaltyCaptions[i].color = on ? UITheme.Red : UITheme.Gold;
            penaltyDays[i].color = on ? UITheme.Cream : UITheme.Cream * 0.55f;
            penaltyNights[i].color = on ? UITheme.Blue : UITheme.Blue * 0.55f;
        }

        FillTradeLines(lines);
        FillStandings(standings);
        FillGuests(guests);
        FillPopular(popular);
    }

    /// 임대료 납부 결과 (기획서 3.2). 부족분은 부채로 이월되므로 완납 여부는 실제로 낸
    /// 금액과 청구액을 비교해 판정한다.
    void BindRent(int day, int owed, int paid, int missStreak)
    {
        targetGoal.text = $"DAY {day:00} RENT";
        targetValue.text = paid.ToString("N0");
        targetGoalText.text = $"/ {owed:N0}";

        var settled = paid >= owed;
        targetValue.color = settled ? UITheme.Green : UITheme.Red;
        targetBadgeBox.GetComponent<UnityEngine.UI.Image>().color =
            settled ? UITheme.Green : UITheme.Red;
        targetBadge.text = settled ? "완납" : "미납";

        penaltyState.text = settled
            ? "완납 — 페널티 적용 받지 않음"
            : $"미납 {missStreak}회 — {Mathf.Clamp(missStreak, 1, PenaltySlots)}단계 적용";
        penaltyState.color = settled ? UITheme.Green : UITheme.Red;
    }

    /// 남은 시간. 전환은 10초라 초 단위로 충분하다 (기획서 4장).
    public void SetRemaining(float seconds, float total)
    {
        if (countdown == null) return;
        countdown.text = Mathf.CeilToInt(Mathf.Max(seconds, 0f)).ToString();
        countdownFill.localScale =
            new Vector3(total > 0f ? Mathf.Clamp01(seconds / total) : 0f, 1f, 1f);
    }

    /// 거래 내역이 흐르는 띠. 합계 구분선(패널 기준 y=235) 위까지가 전부다.
    const float TradeTop = 67f, TradeStep = 41f, TradeRowHeight = 32f, TradeBottom = 230f;

    /// 이 띠에 실제로 들어가는 줄 수. 목업 6번이 4줄로 그려져 있고 자리도 딱 그만큼이다.
    ///
    /// 마지막 줄은 간격이 아니라 자기 높이만큼만 필요하므로 한 번 빼고 나눈 뒤 다시 더한다.
    /// 그냥 (아래 − 위) / 간격 으로 하면 마지막 줄이 들어가는데도 상한이 하나 줄어든다.
    static int TradeCapacity => Mathf.Max(1,
        Mathf.FloorToInt((TradeBottom - TradeTop - TradeRowHeight) / TradeStep) + 1);

    void FillTradeLines(IReadOnlyList<TradeLine> lines)
    {
        Clear(tradeRows);
        if (lines == null || lines.Count == 0) return;

        var capacity = TradeCapacity;
        // 넘치면 마지막 줄을 남은 건수 요약으로 바꾼다. 조용히 버리면 오늘 매출의 근거가
        // 화면에서 사라지는데, 합계는 그대로라 플레이어가 차이를 확인할 방법이 없어진다.
        var overflow = lines.Count > capacity;
        var shown = overflow ? capacity - 1 : lines.Count;

        for (var i = 0; i < shown; i++)
            MakeTradeRow(i, lines[i].Label, lines[i].Amount, lines[i].Tint);

        if (overflow)
            MakeTradeRow(shown, $"그 외 {lines.Count - shown}건", "…", UITheme.Cream);
    }

    void MakeTradeRow(int index, string label, string amount, Color tint)
    {
        var row = new GameObject($"Trade{index}", typeof(RectTransform));
        row.transform.SetParent(tradePanel, false);
        UITheme.At((RectTransform)row.transform, 23f, TradeTop + index * TradeStep,
                   434f, TradeRowHeight);
        tradeRows.Add(row);

        UITheme.Text(row.transform, "Label", label, 12f, UITheme.Cream,
                     0f, 0f, 240f, TradeRowHeight);
        UITheme.Text(row.transform, "Amount", amount, 14f, tint,
                     234f, 0f, 200f, TradeRowHeight, TextAlignmentOptions.TopRight);
    }

    void FillStandings(IReadOnlyList<StandingRow> rows)
    {
        Clear(standingRows);
        if (rows == null || rows.Count == 0) return;

        var topTotal = 0;
        var topToday = 0;
        for (var i = 0; i < rows.Count; i++)
        {
            topTotal = Mathf.Max(topTotal, rows[i].Total);
            topToday = Mathf.Max(topToday, rows[i].Today);
        }
        if (topTotal <= 0) topTotal = 1;
        if (topToday <= 0) topToday = 1;

        for (var i = 0; i < rows.Count; i++)
        {
            var y = 108f + i * 91f;
            if (y + 77f > 845f) break;

            var row = new GameObject($"Standing{i}", typeof(RectTransform));
            row.transform.SetParent(standingPanel, false);
            UITheme.At((RectTransform)row.transform, 23f, y, 366f, 77f);
            standingRows.Add(row);

            UITheme.Text(row.transform, "Rank", $"{i + 1:00}", 9f, UITheme.Gold, 0f, 0f, 27f, 21f);
            UITheme.Text(row.transform, "Cafe", rows[i].Cafe, 14f, UITheme.Cream,
                         29f, 0f, 240f, 28f);
            UITheme.Text(row.transform, "Total", rows[i].Total.ToString("N0"), 16f,
                         rows[i].Mine ? UITheme.GoldLit : UITheme.Cream,
                         166f, 0f, 200f, 36f, TextAlignmentOptions.TopRight);

            var total = UITheme.Bar(row.transform, UITheme.Panel,
                                    rows[i].Mine ? UITheme.GoldLit : UITheme.Cream,
                                    0f, 29f, 366f, 6f);
            total.localScale = new Vector3(Mathf.Clamp01(rows[i].Total / (float)topTotal), 1f, 1f);

            // 오늘 막대는 누적보다 짧게 둔다. 목업이 두 막대의 길이를 다르게 그려
            // 누적과 오늘을 눈으로 구분하게 했다.
            var today = UITheme.Bar(row.transform, UITheme.Panel, UITheme.Blue,
                                    0f, 48f, 300f, 5f);
            today.localScale = new Vector3(Mathf.Clamp01(rows[i].Today / (float)topToday), 1f, 1f);

            UITheme.Text(row.transform, "Today", rows[i].Today.ToString("N0"), 10f, UITheme.Blue,
                         302f, 40f, 64f, 25f, TextAlignmentOptions.TopRight);
        }
    }

    void FillGuests(IReadOnlyList<GuestCard> guests)
    {
        Clear(guestCards);

        for (var i = 0; i < GuestSlots; i++)
        {
            var card = UITheme.Box(forecastPanel, $"Guest{i}", UITheme.PanelDeep,
                                   1f + i * 111.25f, 48f, 110f, 148f);
            guestCards.Add(card.gameObject);

            var has = guests != null && i < guests.Count;
            var race = has && !string.IsNullOrEmpty(guests[i].Race) ? guests[i].Race : "—";
            var count = has ? guests[i].Count : 0;

            UITheme.Box(card, "Portrait", UITheme.Placeholder, 31f, 16f, 48f, 48f);
            UITheme.Text(card, "Race", race, 12f, UITheme.Cream,
                         5f, 72f, 100f, 24f, TextAlignmentOptions.Top);
            UITheme.Text(card, "Count", $"×{count}", 16f, UITheme.GoldLit,
                         5f, 100f, 100f, 36f, TextAlignmentOptions.Top);
        }
    }

    void FillPopular(IReadOnlyList<PopularItem> popular)
    {
        Clear(popularChips);
        if (popular == null) return;

        var count = Mathf.Min(popular.Count, PopularSlots);
        for (var i = 0; i < count; i++)
        {
            var chip = new GameObject($"Popular{i}", typeof(RectTransform));
            chip.transform.SetParent(forecastPanel, false);
            UITheme.At((RectTransform)chip.transform, 190f + i * 230.5f, 215f, 216f, 48f);
            popularChips.Add(chip);

            UITheme.Box(chip.transform, "Icon", UITheme.Placeholder, 13f, 10f, 28f, 28f);
            UITheme.Text(chip.transform, "Name", popular[i].Name, 14f, UITheme.Cream,
                         51f, 13f, 90f, 27f);
            UITheme.Text(chip.transform, "Bonus", $"+{popular[i].Percent}%", 14f, UITheme.Green,
                         158f, 11f, 53f, 30f);
        }
    }

    static void Clear(List<GameObject> pool)
    {
        for (var i = 0; i < pool.Count; i++)
            if (pool[i] != null) Destroy(pool[i]);
        pool.Clear();
    }
}
