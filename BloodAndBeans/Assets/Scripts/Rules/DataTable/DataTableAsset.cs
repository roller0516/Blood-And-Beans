using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

/// 엑셀 열 이름이 필드 이름과 다를 때만 붙인다. `SheetTable.Fill`이 읽는다.
[System.AttributeUsage(System.AttributeTargets.Field)]
public sealed class ColumnAttribute : System.Attribute
{
    public readonly string Name;
    public ColumnAttribute(string name) => Name = name;
}

/// 엑셀 시트 한 장을 열 이름으로 읽는 표. 임포터가 만들어 애셋에 넘긴다.
///
/// 셀은 전부 문자열로 들어온다. 숫자·열거자 변환은 여기서 하고, 실패하면 **어느 시트
/// 몇 행 어느 열인지** 남긴다 — 기획자가 엑셀에서 그 칸을 바로 찾을 수 있어야 한다.
public sealed class SheetTable
{
    public readonly string Name;
    public readonly List<SheetRow> Rows = new();
    public readonly List<string> Errors;

    public SheetTable(string name, List<string> errors)
    {
        Name = name;
        Errors = errors;
    }

    public int Count => Rows.Count;
    public SheetRow this[int index] => Rows[index];

    /// 시트를 행 구조체 목록으로 그대로 옮긴다. **열 이름은 필드 이름이고**, 엑셀 쪽이
    /// 다르면 그 필드에 `[Column("...")]`을 붙인다.
    ///
    /// `key` 칸이 빈 행은 버린다 — 시트 아래에 남은 메모 행이 빈 데이터로 들어오지
    /// 않게 한다. 읽는 타입은 `string`·`int`·`float`·`double`·열거자다.
    public void Fill<T>(List<T> rows, string key) where T : new()
    {
        var schema = Schema(typeof(T));
        rows.Clear();
        foreach (var row in Rows)
        {
            if (!row.Has(key)) continue;

            // 구조체라 한 번 박싱해 두고 필드를 채운 뒤 꺼낸다.
            var boxed = (object)new T();
            foreach (var (field, column) in schema) field.SetValue(boxed, row.Read(field.FieldType, column));
            rows.Add((T)boxed);
        }
    }

    static readonly Dictionary<System.Type, (FieldInfo Field, string Column)[]> schemas = new();

    static (FieldInfo Field, string Column)[] Schema(System.Type type)
    {
        if (schemas.TryGetValue(type, out var cached)) return cached;

        var fields = type.GetFields(BindingFlags.Public | BindingFlags.Instance);
        var schema = new (FieldInfo, string)[fields.Length];
        for (var i = 0; i < fields.Length; i++)
            schema[i] = (fields[i], fields[i].GetCustomAttribute<ColumnAttribute>()?.Name ?? fields[i].Name);

        return schemas[type] = schema;
    }
}

/// 시트의 한 행. `row["열이름"]`으로 읽는다.
public sealed class SheetRow
{
    readonly Dictionary<string, string> cells;
    readonly SheetTable owner;

    /// 엑셀에서의 행 번호. 1부터 세며 머리글 행을 포함한다.
    public readonly int Line;

    public SheetRow(SheetTable owner, int line, Dictionary<string, string> cells)
    {
        this.owner = owner;
        this.cells = cells;
        Line = line;
    }

    void Fail(string column, string value, string want) =>
        owner.Errors.Add($"[{owner.Name}] {Line}행 '{column}' 칸: '{value}'은(는) {want}이(가) 아니다.");

    /// 열 이름은 대소문자를 가리지 않는다. 임포터가 머리글을 소문자로 눕혀 담는다 —
    /// 엑셀에서 `BasePrice`로 적든 `baseprice`로 적든 같은 칸이어야 한다.
    static string Key(string column) => column.Trim().ToLowerInvariant();

    public bool Has(string column) =>
        cells.TryGetValue(Key(column), out var v) && !string.IsNullOrWhiteSpace(v);

    public string Text(string column, string fallback = "") =>
        cells.TryGetValue(Key(column), out var v) && !string.IsNullOrWhiteSpace(v) ? v.Trim() : fallback;

    public float Float(string column, float fallback = 0f)
    {
        if (!Has(column)) return fallback;
        var raw = Text(column);
        if (float.TryParse(raw, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v)) return v;
        Fail(column, raw, "숫자");
        return fallback;
    }

    public double Double(string column, double fallback = 0d)
    {
        if (!Has(column)) return fallback;
        var raw = Text(column);
        if (double.TryParse(raw, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v)) return v;
        Fail(column, raw, "숫자");
        return fallback;
    }

    /// 엑셀이 정수를 `3.0`으로 내주는 경우가 있어 실수로 읽고 반올림한다.
    public int Int(string column, int fallback = 0)
    {
        if (!Has(column)) return fallback;
        var raw = Text(column);
        if (double.TryParse(raw, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, out var v))
            return (int)System.Math.Round(v, System.MidpointRounding.AwayFromZero);
        Fail(column, raw, "정수");
        return fallback;
    }

    /// 열거자 이름으로 읽는다. 오타는 그 칸을 짚어 보고한다 — 조용히 0번 값이 되면
    /// 「우유」가 들어갈 자리에 다른 재료가 들어가고 아무도 모른다.
    public bool Enum<T>(string column, out T value) where T : struct, System.Enum
    {
        value = default;
        if (!Has(column)) return false;

        var raw = Text(column);
        if (System.Enum.TryParse(raw, true, out value)) return true;

        Fail(column, raw, typeof(T).Name);
        return false;
    }

    /// 한 칸에 여러 값을 넣을 때의 구분자는 `|`다. 쉼표를 쓰면 엑셀이 따옴표를 붙여
    /// 사람이 읽을 수 없게 된다.
    public string[] List(string column) =>
        Has(column)
            ? Text(column).Split('|', System.StringSplitOptions.RemoveEmptyEntries)
            : System.Array.Empty<string>();

    /// `|`로 이어 적은 칸이 전부 실제 열거자 이름인지 본다. 읽는 시점에 봐야 어느 행
    /// 어느 칸인지 남는다 — 부팅 때 터지면 누가 오타를 냈는지 알 수 없다.
    public void CheckList<T>(string column) where T : struct, System.Enum
    {
        foreach (var part in List(column))
            if (!System.Enum.TryParse<T>(part.Trim(), true, out _)) Fail(column, part, typeof(T).Name);
    }

    /// `SheetTable.Fill`이 쓰는 반사 경로. 빈 칸은 그 타입의 기본값이고, 값이 있는데
    /// 못 읽는 칸만 오류로 남는다.
    internal object Read(System.Type type, string column)
    {
        if (type == typeof(string)) return Text(column);
        if (type == typeof(int)) return Int(column);
        if (type == typeof(float)) return Float(column);
        if (type == typeof(double)) return Double(column);

        if (type.IsEnum)
        {
            var raw = Text(column);
            if (raw.Length == 0) return System.Enum.ToObject(type, 0);
            if (System.Enum.TryParse(type, raw, true, out var value)) return value;

            Fail(column, raw, type.Name);
            return System.Enum.ToObject(type, 0);
        }

        throw new System.NotSupportedException($"{type.Name}은(는) 시트에서 읽을 수 없는 타입이다.");
    }
}

/// 엑셀 카테고리 한 묶음에 대응하는 데이터 애셋.
///
/// **런타임 진실은 이 애셋이 아니라 `BB.Rules`의 `Balance.Current`다.** 애셋은 저작과
/// 배포용 그릇이고, 부팅 때 `DataManager`가 값을 풀어 `Balance.Load`로 밀어 넣는다.
/// `BB.Rules`가 `noEngineReferences: true`라 `ScriptableObject`를 볼 수 없기 때문이고,
/// 그 덕에 규칙 EditMode 테스트가 씬도 `AssetDatabase`도 없이 그대로 돈다.
public abstract class DataTableAsset : ScriptableObject
{
    /// 임포터 창 왼쪽 버튼에 뜨는 이름.
    public abstract string Category { get; }

    /// 이 애셋이 읽는 엑셀 시트 이름들. 임포터가 이 이름으로 시트를 찾는다.
    public abstract string[] SheetNames { get; }

    /// 시트 한 장을 읽어 담는다. 임포터가 `SheetNames`마다 한 번씩 부른다.
    public abstract void ReadSheet(SheetTable sheet);

    /// 담아 둔 행을 규칙이 읽을 배열로 편다. 부팅 때 `DataManager`가 부른다.
    ///
    /// 값은 이 애셋이 소유한다 — 예전에는 `BalanceData`로 옮겨 담았고, 그래서 같은 수치가
    /// 엑셀과 코드 두 곳에 살았다. 지금은 엑셀 하나뿐이고 규칙이 이 애셋을 직접 읽는다.
    ///
    /// **비어 있는 시트는 건드리지 않는다.** 행이 없는 표까지 덮어쓰면, 한 시트를 빠뜨린
    /// 상태가 다른 시트까지 0으로 만드는 상태가 된다.
    public abstract void Rebuild();
}
