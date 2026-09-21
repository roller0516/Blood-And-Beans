using System.Collections.Generic;
using UnityEngine;

/// 재료의 무게와 표기(기획서 6.7 · 7.1), 손님 종족의 등장 몫과 표기(5.5).
[CreateAssetMenu(menuName = "Blood & Beans/데이터 표/재료·종족", fileName = nameof(NameDataTable))]
public sealed class NameDataTable : DataTableAsset
{
    public const string SheetIngredient = "ingredient";
    public const string SheetRace = "race";

    [System.Serializable]
    public struct IngredientRow
    {
        [Column("ingredient")] public Ingredient Item;
        public float Weight;
        public string Name;
    }

    [System.Serializable]
    public struct RaceRow
    {
        public Race Race;

        /// 등장 가방에서 이 종족이 차지하는 칸 수. 5%가 한 칸이다.
        public int Share;
        public string Name;
    }

    [SerializeField] List<IngredientRow> ingredients = new();

    /// **시트에 적힌 행 순서를 그대로 지킨다.** `Forecast.PickRace`가 이 순서로 만든
    /// 가방을 무작위 위치에서부터 훑어 처음 맞는 종족을 고른다. 열거자 순서로 다시
    /// 세우면 같은 시드가 다른 예보를 낸다.
    [SerializeField] List<RaceRow> races = new();

    public IReadOnlyList<IngredientRow> Ingredients => ingredients;
    public IReadOnlyList<RaceRow> Races => races;

    // --- 규칙이 읽는 값 ---
    //
    // 행에서 펴낸 값이라 직렬화하지 않는다. **코드에 기본값을 두지 않는다** — 수치를
    // 고칠 곳은 엑셀 하나뿐이어야 한다.

    /// 무게. 순서는 `Ingredient` 열거자 0~9와 같다. 보석 전부가 `UpgradePart` 값을 쓴다 (6.7 · 7.1).
    [System.NonSerialized] public float[] ItemWeight = System.Array.Empty<float>();

    /// 7.1 재료 표의 한글 표기. 열거자 순서와 같아야 한다.
    [System.NonSerialized] public string[] ItemNames = System.Array.Empty<string>();

    /// 등장 분포 (기획서 5.5). 5%가 한 칸이다.
    ///
    /// **이 순서가 결과를 바꾼다.** `Forecast.PickRace`가 이 순서로 만든 가방을 무작위
    /// 위치에서부터 훑어 처음 맞는 종족을 고른다. 열거자 순서로 다시 세우면 같은 시드가
    /// 다른 예보를 낸다 — 그래서 시트에 적힌 행 순서를 그대로 지킨다.
    [System.NonSerialized]
    public (Race Race, int Share)[] RaceBagOrder = System.Array.Empty<(Race, int)>();

    /// 5.5 손님 종족 표. `Race` 열거자 순서와 같아야 한다.
    [System.NonSerialized] public string[] RaceNames = System.Array.Empty<string>();

    protected override string DefaultCategory => "재료·종족";
    protected override string[] DefaultSheetNames => new[] { SheetIngredient, SheetRace };

    public override void ReadSheet(int index, SheetTable sheet)
    {
        switch (index)
        {
            case 0: sheet.Fill(ingredients, "ingredient"); break;
            case 1: sheet.Fill(races, "race"); break;
        }
    }

    /// 행을 열거자 순서 배열로 편다. 행이 없는 시트는 빈 배열로 남는다.
    public override void Rebuild()
    {
        if (ingredients.Count > 0)
        {
            var size = 0;
            foreach (var row in ingredients) if ((int)row.Item + 1 > size) size = (int)row.Item + 1;

            var weight = new float[size];
            var names = new string[size];
            foreach (var row in ingredients)
            {
                var i = (int)row.Item;
                if (i < 0 || i >= size) continue;
                weight[i] = row.Weight;
                names[i] = row.Name;
            }
            ItemWeight = weight;
            ItemNames = names;
        }

        if (races.Count > 0)
        {
            // 가방은 시트 순서 그대로, 이름표는 열거자 순서로 — 같은 시트에서 서로 다른
            // 두 표가 나온다.
            var bag = new List<(Race Race, int Share)>(races.Count);
            var size = 0;
            foreach (var row in races) if ((int)row.Race + 1 > size) size = (int)row.Race + 1;

            var names = new string[size];
            foreach (var row in races)
            {
                if (row.Share > 0) bag.Add((row.Race, row.Share));
                if ((int)row.Race >= 0 && (int)row.Race < size) names[(int)row.Race] = row.Name;
            }

            RaceBagOrder = bag.ToArray();
            RaceNames = names;
        }
    }
}
