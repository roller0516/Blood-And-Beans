using System.Collections.Generic;
using UnityEngine;

/// 적재 밴드(6.7) · 상자 칸(6.5.2) · 보석 확률(6.5.2) · 숲 구역과 등급(6.3.1) ·
/// 재료 리젠(6.3.2 · 10장).
[CreateAssetMenu(menuName = "Blood & Beans/데이터 표/밤", fileName = nameof(NightDataTable))]
public sealed class NightDataTable : DataTableAsset
{
    public const string SheetLoadBand = "loadband";
    public const string SheetLootSlot = "lootslot";
    public const string SheetGemChance = "gemchance";
    public const string SheetForestZone = "forestzone";
    public const string SheetForestTier = "foresttier";
    public const string SheetRegen = "regen";
    public const string SheetRegenMap = "regenmap";

    [System.Serializable]
    public struct LoadBandRow
    {
        public int Band;

        /// 이 밴드의 상한. 마지막 밴드는 비운다 — 상한이 없다는 뜻이다.
        public string Max;
        public float Speed;
    }

    [System.Serializable]
    public struct LootSlotRow
    {
        public int Tier;
        public int Min;
        public int Max;
    }

    [System.Serializable]
    public struct GemChanceRow
    {
        public int Day;
        public double Tier2Gem;
        public double Tier3BloodBean;
    }

    [System.Serializable]
    public struct ZoneShareRow
    {
        public ForestRings.Zone Zone;
        public int Share;
    }

    [System.Serializable]
    public struct TierRow
    {
        public ForestRings.Zone Zone;
        public int Day;
        public int T1;
        public int T2;
        public int T3;
    }

    [System.Serializable]
    public struct RegenRow
    {
        public Ingredient Item;

        /// 일차별 가중치를 `35|30|25|...`로 묶는다. 엑셀에서는 `d1`,`d2`… 열로 적고
        /// 임포터가 붙인다 — 일차가 늘어도 코드를 안 고친다.
        public string ByDay;
    }

    [System.Serializable]
    public struct RegenMapRow
    {
        public string MapId;

        /// `Milk|Cream|Berry|Ice`.
        public string Pool;
    }

    [SerializeField] List<LoadBandRow> loadBands = new();
    [SerializeField] List<LootSlotRow> lootSlots = new();
    [SerializeField] List<GemChanceRow> gemChances = new();
    [SerializeField] List<ZoneShareRow> zoneShares = new();
    [SerializeField] List<TierRow> tiers = new();
    [SerializeField] List<RegenRow> regen = new();
    [SerializeField] List<RegenMapRow> regenMaps = new();

    public IReadOnlyList<LoadBandRow> LoadBands => loadBands;
    public IReadOnlyList<LootSlotRow> LootSlots => lootSlots;
    public IReadOnlyList<GemChanceRow> GemChances => gemChances;
    public IReadOnlyList<ZoneShareRow> ZoneShares => zoneShares;
    public IReadOnlyList<TierRow> Tiers => tiers;
    public IReadOnlyList<RegenRow> Regen => regen;
    public IReadOnlyList<RegenMapRow> RegenMaps => regenMaps;

    // --- 규칙이 읽는 값 ---
    //
    // 행에서 펴낸 값이라 직렬화하지 않는다. **코드에 기본값을 두지 않는다** — 수치를
    // 고칠 곳은 엑셀 하나뿐이어야 한다.

    /// 밴드별 이동 속도 배수 (기획서 6.7). 보간이 아니라 밴드 인덱스를 쓰는 이유는 임대료
    /// 페널티가 적재 단계를 정확히 한 칸 낮추기 때문이다 (3.3 밤 항목).
    [System.NonSerialized] public float[] LoadBandSpeed = System.Array.Empty<float>();

    /// 밴드 상한. `ratio < LoadBandMax[i]`인 첫 i가 그 밴드이고, 전부 넘으면 마지막 밴드다.
    [System.NonSerialized] public float[] LoadBandMax = System.Array.Empty<float>();

    /// 등급이 정하는 슬롯 수 범위 (6.5.2 · 6.5.5). 인덱스 0이 1등급이다.
    [System.NonSerialized] public int[] LootSlotMin = System.Array.Empty<int>();
    [System.NonSerialized] public int[] LootSlotMax = System.Array.Empty<int>();

    /// 일차별 2등급 상자의 보석 확률과 3등급 상자의 블러드 빈 확률 (6.5.2).
    [System.NonSerialized] public double[] Tier2GemChance = System.Array.Empty<double>();
    [System.NonSerialized] public double[] Tier3BloodBeanChance = System.Array.Empty<double>();

    /// 구역 배분 퍼센트. 순서는 `ForestRings.Zone`과 같다 (6.3.1).
    [System.NonSerialized] public int[] ZoneSharePercent = System.Array.Empty<int>();

    /// [구역][일차-1] = (1등급, 2등급, 3등급) 퍼센트 (6.3.1 「자리 x 일차」).
    [System.NonSerialized]
    public (int T1, int T2, int T3)[][] TierTable = System.Array.Empty<(int, int, int)[]>();

    /// 흔한 재료의 일차별 가중치 (6.3.2). 0인 날에는 리젠되지 않는다.
    [System.NonSerialized]
    public (Ingredient Item, int[] ByDay)[] RegenWeights = System.Array.Empty<(Ingredient, int[])>();

    /// 맵별 흔한 재료 (기획서 10장).
    [System.NonSerialized]
    public (string MapId, Ingredient[] Pool)[] RegenMapPools = System.Array.Empty<(string, Ingredient[])>();

    public override string Category => "밤";
    public override string[] SheetNames => new[]
    {
        SheetLoadBand, SheetLootSlot, SheetGemChance,
        SheetForestZone, SheetForestTier, SheetRegen, SheetRegenMap,
    };

    public override void ReadSheet(SheetTable sheet)
    {
        switch (sheet.Name)
        {
            case SheetLoadBand: sheet.Fill(loadBands, "band"); break;
            case SheetLootSlot: sheet.Fill(lootSlots, "tier"); break;
            case SheetGemChance: sheet.Fill(gemChances, "day"); break;
            case SheetForestZone: sheet.Fill(zoneShares, "zone"); break;
            case SheetForestTier: sheet.Fill(tiers, "zone"); break;
            case SheetRegen: ReadRegen(sheet); break;

            case SheetRegenMap:
                sheet.Fill(regenMaps, "mapId");
                foreach (var row in sheet.Rows) row.CheckList<Ingredient>("pool");
                break;
        }
    }

    /// 일차 열이 `d1`, `d2`… 로 몇 개인지 시트가 정한다. 열 이름이 고정이 아니라
    /// `Fill`로 옮길 수 없는 유일한 시트다.
    void ReadRegen(SheetTable sheet)
    {
        regen.Clear();
        foreach (var r in sheet.Rows)
        {
            if (!r.Enum<Ingredient>("ingredient", out var item)) continue;

            var days = new List<string>();
            for (var d = 1; r.Has($"d{d}"); d++) days.Add(r.Int($"d{d}").ToString());
            regen.Add(new RegenRow { Item = item, ByDay = string.Join("|", days) });
        }
    }

    /// 행을 열거자·일차 순서 배열로 편다. 행이 없는 시트는 빈 배열로 남는다.
    public override void Rebuild()
    {
        if (loadBands.Count > 0)
        {
            var speed = new float[loadBands.Count];
            var max = new List<float>();
            foreach (var row in loadBands)
            {
                if (row.Band >= 0 && row.Band < speed.Length) speed[row.Band] = row.Speed;

                // 상한이 빈 칸인 행이 마지막 밴드다. 상한 표는 밴드보다 하나 짧다.
                if (!string.IsNullOrWhiteSpace(row.Max) &&
                    float.TryParse(row.Max, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var v)) max.Add(v);
            }
            LoadBandSpeed = speed;
            if (max.Count > 0) LoadBandMax = max.ToArray();
        }

        if (lootSlots.Count > 0)
        {
            var size = Max(lootSlots, r => r.Tier);
            var min = new int[size];
            var maxSlots = new int[size];
            foreach (var row in lootSlots)
            {
                if (row.Tier < 1 || row.Tier > size) continue;
                min[row.Tier - 1] = row.Min;
                maxSlots[row.Tier - 1] = row.Max;
            }
            LootSlotMin = min;
            LootSlotMax = maxSlots;
        }

        if (gemChances.Count > 0)
        {
            var size = Max(gemChances, r => r.Day);
            var gem = new double[size];
            var blood = new double[size];
            foreach (var row in gemChances)
            {
                if (row.Day < 1 || row.Day > size) continue;
                gem[row.Day - 1] = row.Tier2Gem;
                blood[row.Day - 1] = row.Tier3BloodBean;
            }
            Tier2GemChance = gem;
            Tier3BloodBeanChance = blood;
        }

        if (zoneShares.Count > 0)
        {
            var shares = new int[Max(zoneShares, r => (int)r.Zone + 1)];
            foreach (var row in zoneShares)
                if ((int)row.Zone >= 0 && (int)row.Zone < shares.Length) shares[(int)row.Zone] = row.Share;
            ZoneSharePercent = shares;
        }

        if (tiers.Count > 0)
        {
            var zoneCount = Max(tiers, r => (int)r.Zone + 1);
            var dayCount = Max(tiers, r => r.Day);
            var table = new (int T1, int T2, int T3)[zoneCount][];
            for (var z = 0; z < zoneCount; z++) table[z] = new (int, int, int)[dayCount];

            foreach (var row in tiers)
            {
                var z = (int)row.Zone;
                if (z < 0 || z >= zoneCount || row.Day < 1 || row.Day > dayCount) continue;
                table[z][row.Day - 1] = (row.T1, row.T2, row.T3);
            }
            TierTable = table;
        }

        if (regen.Count > 0)
        {
            var rows = new (Ingredient Item, int[] ByDay)[regen.Count];
            for (var i = 0; i < regen.Count; i++) rows[i] = (regen[i].Item, ParseInts(regen[i].ByDay));
            RegenWeights = rows;
        }

        if (regenMaps.Count > 0)
        {
            var rows = new (string MapId, Ingredient[] Pool)[regenMaps.Count];
            for (var i = 0; i < regenMaps.Count; i++) rows[i] = (regenMaps[i].MapId, ParseItems(regenMaps[i].Pool));
            RegenMapPools = rows;
        }
    }

    static int[] ParseInts(string packed)
    {
        if (string.IsNullOrWhiteSpace(packed)) return System.Array.Empty<int>();

        var pieces = packed.Split('|', System.StringSplitOptions.RemoveEmptyEntries);
        var outp = new int[pieces.Length];
        for (var i = 0; i < pieces.Length; i++) int.TryParse(pieces[i].Trim(), out outp[i]);
        return outp;
    }

    static Ingredient[] ParseItems(string packed)
    {
        if (string.IsNullOrWhiteSpace(packed)) return System.Array.Empty<Ingredient>();

        var pieces = packed.Split('|', System.StringSplitOptions.RemoveEmptyEntries);
        var outp = new List<Ingredient>(pieces.Length);
        foreach (var piece in pieces)
            if (System.Enum.TryParse<Ingredient>(piece.Trim(), true, out var item)) outp.Add(item);
        return outp.ToArray();
    }

    static int Max<T>(List<T> rows, System.Func<T, int> pick)
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
