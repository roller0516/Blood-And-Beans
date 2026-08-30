using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 전환 페이즈(10초)의 정산 화면. 오늘의 매출·임대료, 팀 순위, 내일의 손님 예보를
/// 한 화면에 놓는다 (기획서 4장 · 3.2 · 5.6). 레이아웃은 `UI_목업.pptx` 6번이다.
///
/// 이 화면은 값을 만들지 않는다. 얼마를 벌었고 임대료가 얼마인지는 `TeamLedger`·`Rent`가,
/// 내일 손님은 `Forecast`가 정한다. 여기는 받은 값을 정해진 자리에 그리기만 한다.
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

    public readonly struct StandingRow
    {
        public readonly string Cafe;
        public readonly int Revenue;
        public readonly bool Mine;
        public StandingRow(string cafe, int revenue, bool mine)
        {
            Cafe = cafe; Revenue = revenue; Mine = mine;
        }
    }

    /// 예보 카드 한 장. 종족 이름과 선호 태그는 기획서 5.5 표의 문구를 그대로 받는다.
    public readonly struct GuestCard
    {
        public readonly string Race;
        public readonly string Preference;
        public readonly int Count;
        public GuestCard(string race, string preference, int count)
        {
            Race = race; Preference = preference; Count = count;
        }
    }

    public readonly struct PopularItem
    {
        public readonly string Name;
        public readonly int Percent;
        public PopularItem(string name, int percent) { Name = name; Percent = percent; }
    }

    /// 예보 카드와 인기 재료가 들어갈 칸 수. 목업 6번의 가로 폭이 이 개수에 맞춰져 있어서,
    /// 넘치면 자리가 아니라 목업이 바뀌어야 한다. 인기 재료는 기획서 5.6.1의 2~3종과 같다.
    const int GuestSlots = 4;
    const int PopularSlots = 3;

    TMP_Text dayHeading, countdown, todaySales, rentDue, debtCarried, debtNote, partsLabel;
    RectTransform countdownFill;
    Button upgradeButton;
    Transform tradePanel, standingPanel, forecastPanel;

    readonly List<GameObject> tradeRows = new();
    readonly List<GameObject> standingRows = new();
    readonly List<GameObject> guestCards = new();
    readonly List<GameObject> popularChips = new();

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

        UITheme.Box(transform, "Backdrop", UITheme.Ink, 0f, 0f, 1920f, 1080f);

        UITheme.Caption(transform, "INTERMISSION", 48f, 61f, 209f);
        dayHeading = UITheme.Text(transform, "DayHeading", "DAY 00 정산", 32f,
                                  UITheme.Cream, 258f, 36f, 400f, 50f);

        // 전환 10초의 잔여 시간. 막대와 숫자가 같은 값을 본다.
        UITheme.Box(transform, "CountdownTrack", UITheme.Cream, 1410f, 57f, 420f, 6f);
        countdownFill = UITheme.Bar(transform, UITheme.Cream, UITheme.Gold, 1410f, 57f, 420f, 6f);
        countdown = UITheme.Text(transform, "Countdown", "0", 33f, UITheme.Red,
                                 1830f, 38f, 50f, 48f, TextAlignmentOptions.TopRight);

        UITheme.Rule(transform, UITheme.Gold, 48f, 100f, 1824f);

        BuildTradePanel();
        BuildStandingPanel();
        BuildForecastPanel();
        BuildFooter();
    }

    void BuildTradePanel()
    {
        tradePanel = UITheme.Box(transform, "TodaysTrade", UITheme.Panel, 48f, 125f, 472f, 931f);
        UITheme.Caption(tradePanel, "TODAY'S TRADE", 23f, 15f, 440f);
        UITheme.Rule(tradePanel, UITheme.Gold, 1f, 43f, 470f);

        UITheme.Rule(tradePanel, UITheme.Gold, 23f, 208f, 426f);

        UITheme.Text(tradePanel, "SalesLabel", "오늘 매출", 16f, UITheme.Cream,
                     23f, 231f, 102f, 34f);
        todaySales = UITheme.Text(tradePanel, "SalesValue", "0", 24f, UITheme.GoldLit,
                                  260f, 226f, 197f, 41f, TextAlignmentOptions.TopRight);

        UITheme.Text(tradePanel, "RentLabel", "임대료", 16f, UITheme.Cream,
                     23f, 281f, 74f, 34f);
        rentDue = UITheme.Text(tradePanel, "RentValue", "0", 24f, UITheme.Red,
                               260f, 276f, 197f, 41f, TextAlignmentOptions.TopRight);

        UITheme.Rule(tradePanel, UITheme.Gold, 1f, 844f, 470f);
        UITheme.Text(tradePanel, "DebtCaption", "DEBT CARRIED", 8f, UITheme.Red,
                     23f, 882f, 177f, 16f);
        debtNote = UITheme.Text(tradePanel, "DebtNote", string.Empty, 10f, UITheme.Cream,
                                23f, 897f, 260f, 21f);
        debtCarried = UITheme.Text(tradePanel, "DebtValue", "0", 26f, UITheme.Cream,
                                   300f, 861f, 157f, 43f, TextAlignmentOptions.TopRight);
    }

    void BuildStandingPanel()
    {
        standingPanel = UITheme.Box(transform, "Standings", UITheme.Panel, 544f, 125f, 412f, 931f);
        UITheme.Caption(standingPanel, "STANDINGS", 23f, 15f, 378f);
        UITheme.Rule(standingPanel, UITheme.Gold, 1f, 43f, 410f);

        UITheme.Rule(standingPanel, UITheme.Gold, 1f, 875f, 410f);
        UITheme.Text(standingPanel, "Note",
                     "공개되는 것은 매출뿐. 보유 재료 · 설비 · 캐릭터는 비공개.",
                     10f, UITheme.Cream, 23f, 892f, 378f, 26f);
    }

    void BuildForecastPanel()
    {
        forecastPanel = UITheme.Box(transform, "Forecast", UITheme.Panel, 980f, 125f, 892f, 823f);
        UITheme.Caption(forecastPanel, "TOMORROW'S GUESTS", 25f, 17f, 244f);
        UITheme.Text(forecastPanel, "Blind", "어느 박스에 그 재료가 들었는지는 알려주지 않는다",
                     9f, UITheme.Cream, 595f, 15f, 299f, 20f);
        UITheme.Rule(forecastPanel, UITheme.Gold, 1f, 45f, 890f);

        // 카드 사이의 금색 틈. 카드 배경을 이 위에 얹어서 1px 구분선처럼 보이게 한다.
        UITheme.Box(forecastPanel, "GuestStrip", UITheme.Gold, 1f, 46f, 890f, 187f);

        UITheme.Rule(forecastPanel, UITheme.Gold, 1f, 233f, 890f);
        UITheme.Text(forecastPanel, "HotCaption", "HOT INGREDIENTS", 9f, UITheme.Green,
                     25f, 269f, 188f, 18f);

        UITheme.Text(forecastPanel, "Formula",
                     "판매가 = 기본가 × 게이지 × 원두등급 × (1 + 인기 재료 보너스 합)",
                     10f, UITheme.Cream, 25f, 318f, 869f, 21f);
    }

    void BuildFooter()
    {
        var footer = UITheme.Box(transform, "Footer", UITheme.Panel, 980f, 966f, 892f, 90f);
        UITheme.Box(footer, "PartsSwatch", UITheme.Purple, 25f, 31f, 28f, 28f);
        partsLabel = UITheme.Text(footer, "PartsLabel", "업그레이드 재료 ×0 보유", 16f,
                                  UITheme.Cream, 73f, 31f, 253f, 32f);
        UITheme.Text(footer, "PermanentNote", "적용은 그 판 동안 영구", 10f, UITheme.Cream,
                     323f, 37f, 145f, 21f);
        upgradeButton = UITheme.Button(footer, "OpenUpgrade", "업그레이드 열기", true,
                                       627f, 19f, 240f, 52f);
    }

    /// 전환 페이즈가 시작될 때 한 번 부른다. 남은 시간만 계속 바뀌므로 `SetRemaining`으로
    /// 따로 준다 — 여기서 매 프레임 행을 다시 만들면 GC가 붙는다.
    public void Bind(int day,
                     IReadOnlyList<TradeLine> lines, int sales, int rent,
                     int debt, int tomorrowRent,
                     IReadOnlyList<StandingRow> standings,
                     IReadOnlyList<GuestCard> guests,
                     IReadOnlyList<PopularItem> popular,
                     int upgradeParts, Action openUpgrade)
    {
        Build();

        dayHeading.text = $"DAY {day:00} 정산";
        todaySales.text = sales.ToString("N0");
        rentDue.text = rent > 0 ? $"−{rent:N0}" : "0";
        debtCarried.text = debt.ToString("N0");
        debtNote.text = $"이자 없음 · 내일 임대료 {tomorrowRent:N0}";
        partsLabel.text = $"업그레이드 재료 ×{upgradeParts} 보유";

        UIButtons.Wire(upgradeButton, openUpgrade);
        // 재료가 없으면 열 것이 없다. 업그레이드 재료는 3등급 박스 전용이라(기획서 8장)
        // 재료 0으로 이 화면에 오는 판이 흔하다.
        upgradeButton.interactable = upgradeParts > 0 && openUpgrade != null;

        FillTradeLines(lines);
        FillStandings(standings);
        FillGuests(guests);
        FillPopular(popular);
    }

    /// 남은 시간. 전환은 10초라 초 단위로 충분하다 (기획서 4장).
    public void SetRemaining(float seconds, float total)
    {
        if (countdown == null) return;
        countdown.text = Mathf.CeilToInt(Mathf.Max(seconds, 0f)).ToString();
        countdownFill.localScale =
            new Vector3(total > 0f ? Mathf.Clamp01(seconds / total) : 0f, 1f, 1f);
    }

    void FillTradeLines(IReadOnlyList<TradeLine> lines)
    {
        Clear(tradeRows);
        if (lines == null) return;

        for (var i = 0; i < lines.Count; i++)
        {
            var y = 64f + i * 35f;
            // 합계 구분선(y=208) 위까지만 흘린다. 넘치면 오늘 매출과 겹친다.
            if (y + 26f > 200f) break;

            var row = new GameObject($"Trade{i}", typeof(RectTransform));
            row.transform.SetParent(tradePanel, false);
            UITheme.At((RectTransform)row.transform, 23f, y, 434f, 26f);
            tradeRows.Add(row);

            UITheme.Text(row.transform, "Label", lines[i].Label, 12f, UITheme.Cream,
                         0f, 0f, 240f, 26f);
            UITheme.Text(row.transform, "Amount", lines[i].Amount, 14f, lines[i].Tint,
                         234f, 0f, 200f, 26f, TextAlignmentOptions.TopRight);
        }
    }

    void FillStandings(IReadOnlyList<StandingRow> rows)
    {
        Clear(standingRows);
        if (rows == null || rows.Count == 0) return;

        var top = 0;
        for (var i = 0; i < rows.Count; i++) top = Mathf.Max(top, rows[i].Revenue);
        if (top <= 0) top = 1;

        for (var i = 0; i < rows.Count; i++)
        {
            var y = 64f + i * 59f;
            if (y + 41f > 860f) break;

            var row = new GameObject($"Standing{i}", typeof(RectTransform));
            row.transform.SetParent(standingPanel, false);
            UITheme.At((RectTransform)row.transform, 23f, y, 366f, 41f);
            standingRows.Add(row);

            UITheme.Text(row.transform, "Rank", $"{i + 1:00}", 9f, UITheme.Gold,
                         0f, 10f, 27f, 18f);
            UITheme.Text(row.transform, "Cafe", rows[i].Cafe, 14f, UITheme.Cream,
                         29f, 0f, 240f, 30f);
            UITheme.Text(row.transform, "Revenue", rows[i].Revenue.ToString("N0"), 16f,
                         rows[i].Mine ? UITheme.GoldLit : UITheme.Cream,
                         166f, 1f, 200f, 30f, TextAlignmentOptions.TopRight);

            var fill = UITheme.Bar(row.transform, UITheme.Panel,
                                   rows[i].Mine ? UITheme.GoldLit : UITheme.Cream,
                                   0f, 35f, 366f, 6f);
            fill.localScale = new Vector3(Mathf.Clamp01(rows[i].Revenue / (float)top), 1f, 1f);
        }
    }

    void FillGuests(IReadOnlyList<GuestCard> guests)
    {
        Clear(guestCards);
        if (guests == null) return;

        var count = Mathf.Min(guests.Count, GuestSlots);
        for (var i = 0; i < count; i++)
        {
            var card = UITheme.Box(forecastPanel, $"Guest{i}", UITheme.PanelDeep,
                                   1f + i * 223f, 46f, 222f, 187f);
            guestCards.Add(card.gameObject);

            UITheme.Box(card, "Portrait", UITheme.Placeholder, 84f, 18f, 54f, 54f);
            UITheme.Text(card, "Race", guests[i].Race, 14f, UITheme.Cream,
                         11f, 81f, 200f, 30f, TextAlignmentOptions.Top);
            UITheme.Text(card, "Preference", guests[i].Preference, 9f, UITheme.Cream,
                         11f, 116f, 200f, 20f, TextAlignmentOptions.Top);
            UITheme.Text(card, "Count", $"×{guests[i].Count}", 18f, UITheme.GoldLit,
                         11f, 141f, 200f, 32f, TextAlignmentOptions.Top);
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
            UITheme.At((RectTransform)chip.transform, 215f + i * 222f, 252f, 208f, 48f);
            popularChips.Add(chip);

            UITheme.Box(chip.transform, "Icon", UITheme.Placeholder, 13f, 10f, 28f, 28f);
            UITheme.Text(chip.transform, "Name", popular[i].Name, 14f, UITheme.Cream,
                         51f, 12f, 90f, 29f);
            UITheme.Text(chip.transform, "Bonus", $"+{popular[i].Percent}%", 14f, UITheme.Green,
                         149f, 14f, 55f, 25f);
        }
    }

    static void Clear(List<GameObject> pool)
    {
        for (var i = 0; i < pool.Count; i++)
            if (pool[i] != null) Destroy(pool[i]);
        pool.Clear();
    }
}
