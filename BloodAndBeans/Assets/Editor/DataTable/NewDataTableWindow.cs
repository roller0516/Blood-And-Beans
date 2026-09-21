using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// 새 카테고리(왼쪽 탭) 하나를 만드는 폼. 탭 이름·시트 이름·열을 받아 `.cs` 파일을 쓴다.
///
/// **엑셀 시트는 만들지 않는다.** 사람이 직접 추가한다 — 시트가 없으면 임포트할 때
/// 「시트 'customer'이(가) 엑셀에 없다」로 알려 주므로 빠뜨려도 조용히 넘어가지 않는다.
///
/// `Rebuild()`는 비워 둔 채로 남긴다. 읽은 행을 규칙이 어떤 모양으로 볼지는 표마다
/// 다르고, 그 판단은 사람이 한다.
public sealed class NewDataTableWindow : EditorWindow
{
    const string FallbackScriptFolder = "Assets/Scripts/Rules/DataTable";
    const string Suffix = "DataTable";

    [System.Serializable]
    struct Column
    {
        public string Header;   // 엑셀 머리글
        public string Type;     // string · int · float · double · bool · 열거자 이름
    }

    string className = "Customer" + Suffix;
    string category = "손님";
    string sheetName = "customer";
    string summary = "손님 관련 수치 (기획서 5.5).";

    [SerializeField] List<Column> columns = new()
    {
        new Column { Header = "race", Type = "Race" },
        new Column { Header = "patience", Type = "float" },
    };

    Vector2 scroll;
    readonly List<string> problems = new();

    public static void Open()
    {
        var window = GetWindow<NewDataTableWindow>(true, "새 데이터 표", true);
        window.minSize = new Vector2(440f, 420f);
        window.ShowUtility();
    }

    void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);

        EditorGUILayout.LabelField("새 카테고리", EditorStyles.boldLabel);
        className = EditorGUILayout.TextField("클래스 이름", className);
        category = EditorGUILayout.TextField("탭 이름", category);
        sheetName = EditorGUILayout.TextField("엑셀 시트 이름", sheetName);
        summary = EditorGUILayout.TextField("한 줄 설명", summary);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("열 — 맨 위가 키 열이다", EditorStyles.boldLabel);
        EditorGUILayout.LabelField(
            "엑셀에서 키 열 칸이 빈 행은 통째로 버린다. 시트 아래 메모 줄이 빈 데이터로 들어오지 않게 하는 장치다.",
            EditorStyles.wordWrappedMiniLabel);

        var choices = TypeChoices;
        for (var i = 0; i < columns.Count; i++)
        {
            EditorGUILayout.BeginHorizontal();

            var column = columns[i];
            column.Header = EditorGUILayout.TextField(column.Header);

            var at = System.Array.IndexOf(choices, column.Type);
            at = EditorGUILayout.Popup(at < 0 ? 0 : at, choices, GUILayout.Width(140f));
            column.Type = choices[at];
            columns[i] = column;

            using (new EditorGUI.DisabledScope(columns.Count <= 1))
                if (GUILayout.Button("−", GUILayout.Width(24f))) { columns.RemoveAt(i); GUIUtility.ExitGUI(); }

            EditorGUILayout.EndHorizontal();
        }

        if (GUILayout.Button("열 추가")) columns.Add(new Column { Header = "", Type = "float" });

        EditorGUILayout.Space();
        Validate();

        foreach (var problem in problems) EditorGUILayout.HelpBox(problem, MessageType.Error);

        if (problems.Count == 0)
            EditorGUILayout.HelpBox($"{ScriptFolder}/{className}.cs 를 만든다.\n" +
                                    "컴파일이 끝나면 임포터 창에서 「탭 새로 고침」을 누른다.", MessageType.Info);

        using (new EditorGUI.DisabledScope(problems.Count > 0))
            if (GUILayout.Button("만들기", GUILayout.Height(30f))) Create();

        EditorGUILayout.EndScrollView();
    }

    string validated;

    /// 입력이 바뀔 때만 다시 본다. `OnGUI`가 매 프레임 부르는 자리라 `File.Exists`와
    /// 타입 순회를 그대로 두면 창이 떠 있는 내내 디스크를 두드린다.
    void Validate()
    {
        var signature = $"{className}|{category}|{sheetName}|{columns.Count}";
        foreach (var column in columns) signature += $"|{column.Header}:{column.Type}";
        if (signature == validated) return;

        validated = signature;
        problems.Clear();

        if (!IsIdentifier(className)) problems.Add("클래스 이름이 C# 식별자가 아니다.");
        else if (!className.EndsWith(Suffix)) problems.Add($"클래스 이름은 '{Suffix}'로 끝내는 것이 이 저장소의 규칙이다.");
        else if (File.Exists($"{ScriptFolder}/{className}.cs")) problems.Add("같은 이름의 파일이 이미 있다.");
        else if (TypeExists(className)) problems.Add("같은 이름의 타입이 이미 있다.");

        if (string.IsNullOrWhiteSpace(category)) problems.Add("탭 이름이 비어 있다.");
        if (string.IsNullOrWhiteSpace(sheetName)) problems.Add("시트 이름이 비어 있다.");

        var seen = new HashSet<string>();
        foreach (var column in columns)
        {
            if (string.IsNullOrWhiteSpace(column.Header)) { problems.Add("열 이름이 비어 있다."); continue; }

            var field = FieldNameOf(column.Header);
            if (!IsIdentifier(field)) problems.Add($"'{column.Header}'로는 C# 필드 이름을 만들 수 없다.");
            else if (!seen.Add(field)) problems.Add($"'{column.Header}'가 두 번 있다.");
        }
    }

    void Create()
    {
        Directory.CreateDirectory(ScriptFolder);
        var path = $"{ScriptFolder}/{className}.cs";
        File.WriteAllText(path, Build(), new UTF8Encoding(true));

        AssetDatabase.ImportAsset(path);
        CDebug.Log($"데이터 표: {path} 를 만들었다. 컴파일 뒤 「탭 새로 고침」을 누른다.");

        Close();
        AssetDatabase.Refresh();
    }

    string Build()
    {
        var keyField = FieldNameOf(columns[0].Header);
        var text = new StringBuilder();

        text.AppendLine("using System.Collections.Generic;");
        text.AppendLine("using UnityEngine;");
        text.AppendLine();
        text.AppendLine($"/// {summary}");
        text.AppendLine($"[CreateAssetMenu(menuName = \"Blood & Beans/데이터 표/{category}\", fileName = nameof({className}))]");
        text.AppendLine($"public sealed class {className} : DataTableAsset");
        text.AppendLine("{");
        text.AppendLine($"    public const string Sheet = \"{sheetName}\";");
        text.AppendLine();
        text.AppendLine("    [System.Serializable]");
        text.AppendLine("    public struct Row");
        text.AppendLine("    {");

        foreach (var column in columns)
        {
            var field = FieldNameOf(column.Header);
            var attribute = field == column.Header ? string.Empty : $"[Column(\"{column.Header}\")] ";
            text.AppendLine($"        {attribute}public {column.Type} {field};");
        }

        text.AppendLine("    }");
        text.AppendLine();
        text.AppendLine("    [SerializeField] List<Row> rows = new();");
        text.AppendLine();
        text.AppendLine("    public IReadOnlyList<Row> Rows => rows;");
        text.AppendLine();
        text.AppendLine($"    protected override string DefaultCategory => \"{category}\";");
        text.AppendLine("    protected override string[] DefaultSheetNames => new[] { Sheet };");
        text.AppendLine();
        text.AppendLine($"    public override void ReadSheet(int index, SheetTable sheet) => sheet.Fill(rows, \"{keyField}\");");
        text.AppendLine();
        text.AppendLine("    /// 읽은 행을 규칙이 읽을 모양으로 편다.");
        text.AppendLine("    ///");
        text.AppendLine("    /// ponytail: 아직 비어 있다. 무엇을 어떤 배열로 펼지는 표마다 달라 생성기가");
        text.AppendLine("    /// 정하지 못한다. `NameDataTable.Rebuild`가 열거자 인덱스 배열의 예다.");
        text.AppendLine("    public override void Rebuild()");
        text.AppendLine("    {");
        text.AppendLine("    }");
        text.AppendLine("}");

        return text.ToString();
    }

    static string scriptFolder;

    /// 스크립트를 둘 폴더. 기존 표가 사는 곳을 따라간다 — 폴더가 옮겨져도 같이 따라가야
    /// 새 표만 엉뚱한 자리에 생기지 않는다.
    ///
    /// 한 번 찾아 캐시한다. `OnGUI`가 매 프레임 부르는 자리라 애셋 조회를 그대로 두면
    /// 창이 떠 있는 내내 인스턴스를 만들고 버린다.
    static string ScriptFolder => scriptFolder ??= FindScriptFolder();

    static string FindScriptFolder()
    {
        var probe = CreateInstance<CommonDataTable>();
        var script = MonoScript.FromScriptableObject(probe);
        var path = script != null ? AssetDatabase.GetAssetPath(script) : null;
        DestroyImmediate(probe);

        return string.IsNullOrEmpty(path)
            ? FallbackScriptFolder
            : Path.GetDirectoryName(path).Replace('\\', '/');
    }

    /// 엑셀 머리글을 C# 필드 이름으로. 첫 글자만 올리고 식별자에 못 쓰는 글자는 뺀다.
    /// 머리글과 달라지면 `[Column]`이 붙으므로 엑셀은 그대로 둬도 된다.
    static string FieldNameOf(string header)
    {
        if (string.IsNullOrWhiteSpace(header)) return string.Empty;

        var text = new StringBuilder();
        foreach (var ch in header.Trim())
            if (char.IsLetterOrDigit(ch) || ch == '_') text.Append(ch);

        if (text.Length == 0) return string.Empty;
        text[0] = char.ToUpperInvariant(text[0]);
        return text.ToString();
    }

    static bool IsIdentifier(string name)
    {
        if (string.IsNullOrEmpty(name) || (!char.IsLetter(name[0]) && name[0] != '_')) return false;

        foreach (var ch in name)
            if (!char.IsLetterOrDigit(ch) && ch != '_') return false;
        return true;
    }

    static bool TypeExists(string name)
    {
        foreach (var type in TypeCache.GetTypesDerivedFrom<ScriptableObject>())
            if (type.Name == name) return true;
        return false;
    }

    static string[] typeChoices;

    /// 고를 수 있는 열 타입. 기본형에 규칙 어셈블리의 공개 열거자를 붙인다 —
    /// 타입 이름을 손으로 적게 두면 오타가 컴파일 오류로만 드러난다.
    static string[] TypeChoices
    {
        get
        {
            if (typeChoices != null) return typeChoices;

            var list = new List<string> { "string", "int", "float", "double", "bool" };
            foreach (var type in typeof(Ingredient).Assembly.GetTypes())
                if (type.IsEnum && type.IsPublic) list.Add(type.Name);

            return typeChoices = list.ToArray();
        }
    }
}
