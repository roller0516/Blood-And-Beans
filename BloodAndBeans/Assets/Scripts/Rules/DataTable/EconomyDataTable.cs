using System.Collections.Generic;
using UnityEngine;

/// 임대료(기획서 3.2) · 미납 페널티(3.3) · 메뉴(7.2) · 완성 게이지 배수(5.2).
[CreateAssetMenu(menuName = "Blood & Beans/데이터 표/경제", fileName = nameof(EconomyDataTable))]
public sealed class EconomyDataTable : DataTableAsset
{
    public const string SheetRent = "rent";
    public const string SheetPenalty = "penalty";
    public const string SheetMenu = "menu";
    public const string SheetGauge = "gauge";

    [System.Serializable]
    public struct RentRow
    {
        public int Day;
        public int Due;
    }

    [System.Serializable]
    public struct PenaltyRow
    {
        /// 0=미납 없음, 1~3=연속 미납 횟수.
        public int Tier;
        public float CraftLoss;
        public float MoveLoss;
        public float VisionLoss;
        public float OpenLoss;
    }

    [System.Serializable]
    public struct MenuRow
    {
        public MenuId Menu;
        public int BasePrice;

        /// `Bean|Milk|Ice`. 쉼표를 쓰면 엑셀이 따옴표를 붙인다.
        public string Parts;
        public string Name;
    }

    [System.Serializable]
    public struct GaugeRow
    {
        public Gauge Gauge;
        public float Multiplier;
    }

    [SerializeField] List<RentRow> rent = new();
    [SerializeField] List<PenaltyRow> penalty = new();
    [SerializeField] List<MenuRow> menus = new();
    [SerializeField] List<GaugeRow> gauges = new();

    public IReadOnlyList<RentRow> Rent => rent;
    public IReadOnlyList<PenaltyRow> Penalty => penalty;
    public IReadOnlyList<MenuRow> Menus => menus;
    public IReadOnlyList<GaugeRow> Gauges => gauges;

    // --- 규칙이 읽는 값 ---
    //
    // 행에서 펴낸 값이라 직렬화하지 않는다. **코드에 기본값을 두지 않는다** — 수치를
    // 고칠 곳은 엑셀 하나뿐이어야 한다.

    /// 일차별 임대료 (기획서 3.2). 표를 넘는 일차는 마지막 값으로 고정한다.
    [System.NonSerialized] public int[] RentByDay = System.Array.Empty<int>();

    // 미납 단계(None·1회·2회·3회)별 감소율 (기획서 3.3). 보석과 같은 축에 마이너스로 붙는다.
    [System.NonSerialized] public float[] PenaltyCraftLoss = System.Array.Empty<float>();   // 낮: 제작 속도
    [System.NonSerialized] public float[] PenaltyMoveLoss = System.Array.Empty<float>();    // 낮: 이동 속도
    [System.NonSerialized] public float[] PenaltyVisionLoss = System.Array.Empty<float>();  // 밤: 시야 반경
    [System.NonSerialized] public float[] PenaltyOpenLoss = System.Array.Empty<float>();    // 밤: 박스 개봉 속도

    /// 기본가와 조합 (기획서 7.2). 조합법 표시와 판매 정산이 같은 표를 쓴다.
    [System.NonSerialized] public MenuDef[] MenuTable = System.Array.Empty<MenuDef>();

    /// 7.2 메뉴 표의 한글 표기. `MenuId` 열거자 순서와 같아야 한다.
    [System.NonSerialized] public string[] MenuNames = System.Array.Empty<string>();

    /// 순서는 `Gauge` 열거자와 같다: Perfect·Good·Miss·Burnt (기획서 5.2).
    [System.NonSerialized] public float[] GaugeMultiplier = System.Array.Empty<float>();

    public override string Category => "경제";
    public override string[] SheetNames => new[] { SheetRent, SheetPenalty, SheetMenu, SheetGauge };

    public override void ReadSheet(SheetTable sheet)
    {
        switch (sheet.Name)
        {
            case SheetRent: sheet.Fill(rent, "day"); break;
            case SheetPenalty: sheet.Fill(penalty, "tier"); break;
            case SheetGauge: sheet.Fill(gauges, "gauge"); break;

            case SheetMenu:
                sheet.Fill(menus, "menu");

                // 조합이 실제 재료 이름인지 읽는 시점에 본다. 부팅 때 터지면 누가 오타를 냈는지 남지 않는다.
                foreach (var row in sheet.Rows) row.CheckList<Ingredient>("parts");
                break;
        }
    }

    /// 행을 열거자·일차 순서 배열로 편다. 행이 없는 시트는 빈 배열로 남는다.
    public override void Rebuild()
    {
        if (rent.Count > 0)
        {
            var table = new int[MaxOf(rent, r => r.Day)];
            foreach (var row in rent)
                if (row.Day >= 1 && row.Day <= table.Length) table[row.Day - 1] = row.Due;
            RentByDay = table;
        }

        if (penalty.Count > 0)
        {
            var size = MaxOf(penalty, p => p.Tier + 1);
            var craft = new float[size];
            var move = new float[size];
            var vision = new float[size];
            var open = new float[size];
            foreach (var row in penalty)
            {
                if (row.Tier < 0 || row.Tier >= size) continue;
                craft[row.Tier] = row.CraftLoss;
                move[row.Tier] = row.MoveLoss;
                vision[row.Tier] = row.VisionLoss;
                open[row.Tier] = row.OpenLoss;
            }
            PenaltyCraftLoss = craft;
            PenaltyMoveLoss = move;
            PenaltyVisionLoss = vision;
            PenaltyOpenLoss = open;
        }

        if (menus.Count > 0)
        {
            var defs = new MenuDef[menus.Count];
            var names = new string[MaxOf(menus, m => (int)m.Menu + 1)];
            for (var i = 0; i < menus.Count; i++)
            {
                var row = menus[i];
                defs[i] = new MenuDef(row.Menu, row.BasePrice, ParseParts(row.Parts));
                if ((int)row.Menu >= 0 && (int)row.Menu < names.Length) names[(int)row.Menu] = row.Name;
            }
            MenuTable = defs;
            MenuNames = names;
        }

        if (gauges.Count > 0)
        {
            var table = new float[MaxOf(gauges, g => (int)g.Gauge + 1)];
            foreach (var row in gauges)
                if ((int)row.Gauge >= 0 && (int)row.Gauge < table.Length) table[(int)row.Gauge] = row.Multiplier;
            GaugeMultiplier = table;
        }
    }

    static Ingredient[] ParseParts(string packed)
    {
        if (string.IsNullOrWhiteSpace(packed)) return System.Array.Empty<Ingredient>();

        var pieces = packed.Split('|', System.StringSplitOptions.RemoveEmptyEntries);
        var outp = new List<Ingredient>(pieces.Length);
        foreach (var piece in pieces)
            if (System.Enum.TryParse<Ingredient>(piece.Trim(), true, out var item)) outp.Add(item);
        return outp.ToArray();
    }

    static int MaxOf<T>(List<T> rows, System.Func<T, int> pick)
    {
        var best = 0;
        foreach (var row in rows)
        {
            var v = pick(row);
            if (v > best) best = v;
        }
        return best;
    }
}
