using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// 엑셀 한 권을 데이터 표 애셋으로 옮긴다. 창(`DataTableWindow`)과 메뉴가 이것을 부른다.
///
/// **한 애셋은 전부 들어가거나 하나도 안 들어간다.** 임시 인스턴스에 먼저 읽고, 오류가
/// 없을 때만 진짜 애셋에 복사한다 — 반쯤 들어간 표가 제일 나쁘다. 오타 하나 때문에
/// 임대료만 새 값이고 페널티는 옛 값인 상태로 판이 돌면 원인을 찾을 수가 없다.
public static class DataTableImporter
{
    public const string DefaultExcelPath = "Assets/Excel/BloodAndBeans.xlsx";
    public const string ManagerAssetPath = "Assets/Resources/DataManager.asset";
    public const string TableFolder = "Assets/Resources/DataTable";

    const string PathPrefsKey = "BloodAndBeans.DataTable.ExcelPath";

    /// 엑셀 원본 경로. 창에서 바꿀 수 있고 사람마다 따로 기억한다.
    public static string ExcelPath
    {
        get => EditorPrefs.GetString(PathPrefsKey, DefaultExcelPath);
        set => EditorPrefs.SetString(PathPrefsKey, string.IsNullOrWhiteSpace(value) ? DefaultExcelPath : value);
    }

    public sealed class Report
    {
        public readonly List<string> Errors = new();
        public readonly List<string> Notes = new();
        public bool Ok => Errors.Count == 0;
    }

    /// 엑셀을 읽어 애셋에 넣는다. `only`가 null이면 전체 임포트다.
    public static Report Import(DataManager manager, DataTableAsset only = null)
    {
        var report = new Report();

        if (manager == null)
        {
            report.Errors.Add($"{ManagerAssetPath} 애셋이 없다. 창의 「설정 만들기」를 먼저 눌러 달라.");
            return report;
        }

        var book = XlsxReader.Read(ExcelPath, out var error);
        if (book == null)
        {
            report.Errors.Add(error);
            return report;
        }

        var imported = 0;
        foreach (var table in manager.Tables)
        {
            if (table == null) continue;
            if (only != null && table != only) continue;

            if (ImportOne(table, book, report)) imported++;
        }

        if (imported > 0)
        {
            AssetDatabase.SaveAssets();

            // 에디터가 곧바로 새 수치를 보게 한다. 임포트하고 재생을 눌러야 반영되면
            // 기획자가 값을 확인하려고 매번 재생해야 한다.
            manager.Apply();
        }

        report.Notes.Add($"표 {imported}개를 넣었다. 엑셀: {ExcelPath}");
        return report;
    }

    /// 애셋 하나를 채운다. 성공하면 true.
    static bool ImportOne(DataTableAsset target, Dictionary<string, XlsxReader.Sheet> book, Report report)
    {
        // 임시 인스턴스에 먼저 읽는다. 오류가 나면 진짜 애셋은 건드리지 않은 채로 남는다.
        var scratch = (DataTableAsset)ScriptableObject.CreateInstance(target.GetType());
        var errors = new List<string>();
        var read = 0;

        foreach (var sheetName in target.SheetNames)
        {
            if (!book.TryGetValue(sheetName, out var raw))
            {
                errors.Add($"[{target.Category}] 시트 '{sheetName}'이(가) 엑셀에 없다.");
                continue;
            }

            var sheet = ToSheetTable(raw, errors);
            if (sheet == null)
            {
                errors.Add($"[{sheetName}] 머리글 행이 없다. 첫 행에 열 이름을 적어 달라.");
                continue;
            }

            scratch.ReadSheet(sheet);
            read++;
        }

        if (errors.Count > 0)
        {
            report.Errors.AddRange(errors);
            Object.DestroyImmediate(scratch);
            return false;
        }

        EditorUtility.CopySerialized(scratch, target);
        EditorUtility.SetDirty(target);
        Object.DestroyImmediate(scratch);

        report.Notes.Add($"[{target.Category}] 시트 {read}장을 읽었다.");
        return true;
    }

    /// 첫 번째 비어 있지 않은 행을 머리글로 본다. 열 이름은 대소문자를 가리지 않는다.
    static SheetTable ToSheetTable(XlsxReader.Sheet raw, List<string> errors)
    {
        var headerAt = -1;
        for (var i = 0; i < raw.Rows.Count; i++)
        {
            if (raw.Rows[i].Exists(cell => !string.IsNullOrWhiteSpace(cell))) { headerAt = i; break; }
        }
        if (headerAt < 0) return null;

        var header = raw.Rows[headerAt];
        var columns = new string[header.Count];
        for (var c = 0; c < header.Count; c++) columns[c] = (header[c] ?? string.Empty).Trim().ToLowerInvariant();

        var table = new SheetTable(raw.Name, errors);
        for (var i = headerAt + 1; i < raw.Rows.Count; i++)
        {
            var cells = raw.Rows[i];
            if (!cells.Exists(cell => !string.IsNullOrWhiteSpace(cell))) continue;   // 빈 행은 건너뛴다

            var map = new Dictionary<string, string>();
            for (var c = 0; c < columns.Length && c < cells.Count; c++)
            {
                if (columns[c].Length == 0) continue;
                map[columns[c]] = cells[c];
            }
            table.Rows.Add(new SheetRow(table, raw.Lines[i], map));
        }
        return table;
    }

    // --- 설정 만들기 ---

    public static DataManager LoadManager() =>
        AssetDatabase.LoadAssetAtPath<DataManager>(ManagerAssetPath);

    /// 매니저와 카테고리 애셋을 없으면 만든다. 이미 있으면 그대로 쓴다.
    ///
    /// **`DataTableAsset`을 상속한 타입을 전부 찾아낸다.** 카테고리를 새로 만들 때
    /// 클래스만 쓰고 이 버튼을 누르면 되고, 여기 목록을 고칠 일이 없다.
    ///
    /// 이미 이어져 있는 것은 **순서를 지킨다** — 왼쪽 탭 순서는 사람이 정한 것이라
    /// 고칠 때마다 뒤섞으면 안 된다. 새 타입만 뒤에 붙는다.
    public static DataManager CreateOrRepairSetup()
    {
        EnsureFolder("Assets/Resources");
        EnsureFolder(TableFolder);
        EnsureFolder(Path.GetDirectoryName(DefaultExcelPath).Replace('\\', '/'));

        var manager = LoadManager();
        if (manager == null)
        {
            manager = ScriptableObject.CreateInstance<DataManager>();
            AssetDatabase.CreateAsset(manager, ManagerAssetPath);
        }

        var tables = new List<DataTableAsset>();
        var seen = new HashSet<System.Type>();

        // 이미 이어 둔 것부터. 순서와, 사람이 옮겨 둔 애셋 경로를 그대로 둔다.
        foreach (var existing in manager.Tables)
        {
            if (existing == null || !seen.Add(existing.GetType())) continue;
            tables.Add(existing);
        }

        foreach (var type in TypeCache.GetTypesDerivedFrom<DataTableAsset>())
        {
            if (type.IsAbstract || !seen.Add(type)) continue;

            // 애셋 파일이 이미 있으면 그것을 쓴다. 없을 때만 새로 만든다.
            var path = $"{TableFolder}/{type.Name}.asset";
            var asset = AssetDatabase.LoadAssetAtPath<DataTableAsset>(path) ?? FindAnywhere(type);
            if (asset == null)
            {
                asset = (DataTableAsset)ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(asset, path);
            }
            tables.Add(asset);
        }

        var serialized = new SerializedObject(manager);
        var list = serialized.FindProperty("tables");
        list.arraySize = tables.Count;
        for (var i = 0; i < tables.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = tables[i];
        serialized.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(manager);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        return manager;
    }

    /// 애셋을 다른 폴더로 옮겨 뒀을 수도 있다. 이름이 아니라 타입으로 찾는다.
    static DataTableAsset FindAnywhere(System.Type type)
    {
        foreach (var guid in AssetDatabase.FindAssets($"t:{type.Name}"))
        {
            var found = AssetDatabase.LoadAssetAtPath<DataTableAsset>(AssetDatabase.GUIDToAssetPath(guid));
            if (found != null && found.GetType() == type) return found;
        }
        return null;
    }

    static void EnsureFolder(string path)
    {
        if (string.IsNullOrEmpty(path) || AssetDatabase.IsValidFolder(path)) return;

        var parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(parent) && parent != "Assets") EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
