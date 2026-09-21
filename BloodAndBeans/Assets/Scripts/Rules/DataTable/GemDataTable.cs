using System.Collections.Generic;
using UnityEngine;

/// 보석 6종의 배수·재료·이름·효과 (기획서 8.2). 지속 턴 수와 드롭 확률은 여기가 아니다 —
/// 턴은 공통 수치, 확률은 밤 표에 있다 (6.5.2).
[CreateAssetMenu(menuName = "Blood & Beans/데이터 표/보석", fileName = nameof(GemDataTable))]
public sealed class GemDataTable : DataTableAsset
{
    public const string SheetGem = "gem";

    [System.Serializable]
    public struct Row
    {
        public Gem Gem;

        /// 그 보석이 곱하는 값. 어디에 곱하는지는 `Gems`의 프로퍼티가 정한다.
        public float Scale;

        /// 이 보석으로 세는 재료. 열거자 값이 연속이 아니라 캐스팅으로 대신할 수 없다.
        public Ingredient Item;
        public string Name;
        public string Effect;
    }

    [SerializeField] List<Row> rows = new();

    // --- 규칙이 읽는 값 (기획서 8.2) ---
    //
    // `rows`에서 펴낸 값이라 직렬화하지 않는다. **코드에 기본값을 두지 않는다** — 수치를
    // 고칠 곳은 엑셀 하나뿐이어야 한다. 순서는 `Gem` 열거자와 같다.

    [System.NonSerialized] public float[] Scale = System.Array.Empty<float>();
    [System.NonSerialized] public string[] Names = System.Array.Empty<string>();
    [System.NonSerialized] public string[] Effects = System.Array.Empty<string>();

    /// `Gem`을 재료로 바꾸는 표. 열거자 값이 연속이 아니라(WindGem=10 ... TeaGem=15)
    /// 캐스팅으로 대신할 수 없다.
    [System.NonSerialized] public Ingredient[] Items = System.Array.Empty<Ingredient>();

    public IReadOnlyList<Row> Rows => rows;
    protected override string DefaultCategory => "보석";
    protected override string[] DefaultSheetNames => new[] { SheetGem };

    public override void ReadSheet(int index, SheetTable sheet) => sheet.Fill(rows, "gem");

    /// 행을 열거자 순서 배열로 편다. 행이 없으면 빈 배열로 남는다.
    public override void Rebuild()
    {
        if (rows.Count == 0) return;

        var size = 0;
        foreach (var row in rows) if ((int)row.Gem + 1 > size) size = (int)row.Gem + 1;

        var scale = new float[size];
        var items = new Ingredient[size];
        var names = new string[size];
        var effects = new string[size];

        foreach (var row in rows)
        {
            var i = (int)row.Gem;
            if (i < 0 || i >= size) continue;
            scale[i] = row.Scale;
            items[i] = row.Item;
            names[i] = row.Name;
            effects[i] = row.Effect;
        }

        Scale = scale;
        Items = items;
        Names = names;
        Effects = effects;
    }
}
