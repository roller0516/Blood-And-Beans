using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// 엑셀을 데이터 표 애셋으로 넣는 창. `Blood & Beans > 데이터 표 임포터`.
///
/// 왼쪽이 카테고리, 오른쪽이 그 카테고리의 실제 데이터다. 카테고리마다 임포트 버튼이
/// 따로 있고 위에 전체 임포트가 있다 — 캐릭터 수치만 만졌을 때 숲 표까지 다시 넣을
/// 이유가 없다.
public sealed class DataTableWindow : EditorWindow
{
    /// 분할선이 물러설 수 있는 한계. 카테고리 이름이 잘리거나 오른쪽 표가 못 읽을 만큼
    /// 좁아지는 것을 막는다.
    const float CategoryHeight = 34f;
    const float MinSideWidth = 96f;
    const float MinBodyWidth = 280f;
    const float SplitterWidth = 5f;

    /// 왼쪽 목록 폭. 창과 함께 기억된다.
    [SerializeField] float sideWidth = 148f;

    bool draggingSplitter;

    [SerializeField] int selected;
    [SerializeField] Vector2 sideScroll;
    [SerializeField] Vector2 bodyScroll;
    [SerializeField] Vector2 reportScroll;

    DataManager manager;
    readonly Dictionary<Object, Editor> inspectors = new();
    DataTableImporter.Report report;

    [MenuItem("Blood & Beans/데이터 표 임포터", priority = 20)]
    public static void Open() => GetWindow<DataTableWindow>("데이터 표").minSize = new Vector2(720f, 420f);

    /// 창을 열지 않고 한 번에 넣는다. 엑셀을 고치고 바로 확인할 때 쓴다.
    [MenuItem("Blood & Beans/데이터 표 전체 임포트", priority = 21)]
    public static void ImportAllFromMenu()
    {
        var result = DataTableImporter.Import(DataTableImporter.LoadManager());
        LogReport(result);
    }

    void OnDisable()
    {
        foreach (var editor in inspectors.Values) if (editor != null) DestroyImmediate(editor);
        inspectors.Clear();
    }

    void OnGUI()
    {
        if (manager == null) manager = DataTableImporter.LoadManager();

        DrawToolbar();

        if (manager == null)
        {
            EditorGUILayout.HelpBox(
                $"{DataTableImporter.ManagerAssetPath} 애셋이 없다.\n「설정 만들기」를 누르면 매니저와 카테고리 애셋 6장, 엑셀 폴더를 만든다.",
                MessageType.Info);

            if (GUILayout.Button("설정 만들기", GUILayout.Height(28f)))
            {
                manager = DataTableImporter.CreateOrRepairSetup();
                GUIUtility.ExitGUI();
            }
            return;
        }

        EditorGUILayout.BeginHorizontal();
        DrawCategories();
        DrawSplitter();
        DrawBody();
        EditorGUILayout.EndHorizontal();

        DrawReport();
    }

    void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        EditorGUILayout.LabelField("엑셀", GUILayout.Width(32f));
        var path = EditorGUILayout.TextField(DataTableImporter.ExcelPath, EditorStyles.toolbarTextField);
        if (path != DataTableImporter.ExcelPath) DataTableImporter.ExcelPath = path;

        if (GUILayout.Button("찾아보기", EditorStyles.toolbarButton, GUILayout.Width(64f)))
        {
            var picked = EditorUtility.OpenFilePanel("밸런스 엑셀 고르기", "Assets", "xlsx");
            if (!string.IsNullOrEmpty(picked)) DataTableImporter.ExcelPath = ToProjectPath(picked);
            GUIUtility.ExitGUI();
        }

        if (GUILayout.Button("열기", EditorStyles.toolbarButton, GUILayout.Width(40f)))
            Application.OpenURL("file:///" + System.IO.Path.GetFullPath(DataTableImporter.ExcelPath));

        GUILayout.FlexibleSpace();

        // `DataTableAsset`을 상속한 클래스를 새로 쓰면 이걸 눌러 탭으로 끌어온다.
        // 기존 탭의 순서와 내용은 건드리지 않는다.
        if (GUILayout.Button("탭 새로 고침", EditorStyles.toolbarButton, GUILayout.Width(84f)))
        {
            manager = DataTableImporter.CreateOrRepairSetup();
            GUIUtility.ExitGUI();
        }

        if (GUILayout.Button("전체 임포트", EditorStyles.toolbarButton, GUILayout.Width(80f)))
            Run(null);

        EditorGUILayout.EndHorizontal();
    }

    /// 왼쪽과 오른쪽 사이의 드래그 손잡이.
    ///
    /// `GUIUtility.hotControl`을 잡는 이유는 드래그 도중 커서가 창 밖으로 나가도 놓을 때까지
    /// 이 컨트롤이 이벤트를 계속 받게 하기 위해서다. 안 잡으면 창 밖에서 버튼을 떼는 순간
    /// `MouseUp`이 안 와서 선이 커서를 영원히 따라다닌다.
    void DrawSplitter()
    {
        var rect = GUILayoutUtility.GetRect(SplitterWidth, SplitterWidth,
            GUILayout.Width(SplitterWidth), GUILayout.ExpandHeight(true));

        EditorGUI.DrawRect(rect, draggingSplitter ? DragColor : LineColor);
        EditorGUIUtility.AddCursorRect(rect, MouseCursor.ResizeHorizontal);

        var id = GUIUtility.GetControlID(FocusType.Passive);
        var e = Event.current;

        switch (e.GetTypeForControl(id))
        {
            case EventType.MouseDown when e.button == 0 && rect.Contains(e.mousePosition):
                draggingSplitter = true;
                GUIUtility.hotControl = id;
                e.Use();
                break;

            case EventType.MouseDrag when draggingSplitter:
                sideWidth = ClampSide(e.mousePosition.x - SplitterWidth * 0.5f);
                e.Use();
                Repaint();
                break;

            case EventType.MouseUp when draggingSplitter:
                draggingSplitter = false;
                GUIUtility.hotControl = 0;
                e.Use();
                break;
        }

        // 창을 줄이면 선이 밖으로 밀려난다. 그릴 때마다 한계 안으로 되돌린다.
        if (!draggingSplitter) sideWidth = ClampSide(sideWidth);
    }

    /// 창이 아주 좁으면 최소 폭 둘을 다 지킬 수 없다. 그때는 왼쪽을 우선한다 —
    /// 카테고리를 못 고르면 오른쪽이 넓어도 아무 소용이 없다.
    float ClampSide(float want) =>
        Mathf.Clamp(want, MinSideWidth, Mathf.Max(MinSideWidth, position.width - MinBodyWidth));

    static Color LineColor => EditorGUIUtility.isProSkin
        ? new Color(0.14f, 0.14f, 0.14f) : new Color(0.60f, 0.60f, 0.60f);

    static Color DragColor => EditorGUIUtility.isProSkin
        ? new Color(0.35f, 0.55f, 0.85f) : new Color(0.25f, 0.45f, 0.80f);

    void DrawCategories()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(sideWidth));
        sideScroll = EditorGUILayout.BeginScrollView(sideScroll, GUILayout.Width(sideWidth));

        var tables = manager.Tables;
        for (var i = 0; i < tables.Length; i++)
        {
            var table = tables[i];
            var label = table != null ? table.Category : "(비어 있음)";

            // 고른 칸과 안 고른 칸이 **같은 스타일**이어야 한다. 스타일을 갈아 끼우면
            // 테두리·여백이 달라져 고른 칸만 크기가 변한다.
            var on = GUILayout.Toggle(i == selected, label, CategoryStyle, GUILayout.Height(CategoryHeight));
            if (on && i != selected)
            {
                selected = i;
                bodyScroll = Vector2.zero;
            }
        }

        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    void DrawBody()
    {
        EditorGUILayout.BeginVertical();

        var tables = manager.Tables;
        if (selected < 0 || selected >= tables.Length || tables[selected] == null)
        {
            EditorGUILayout.HelpBox("이 칸에 데이터 표 애셋이 이어져 있지 않다. 「설정 만들기」로 채울 수 있다.", MessageType.Warning);
            if (GUILayout.Button("설정 만들기")) { manager = DataTableImporter.CreateOrRepairSetup(); GUIUtility.ExitGUI(); }
            EditorGUILayout.EndVertical();
            return;
        }

        var table = tables[selected];

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(table.Category, EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        EditorGUILayout.LabelField("시트 : " + string.Join(", ", table.SheetNames), EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("이 표 임포트", GUILayout.Width(96f))) Run(table);
        EditorGUILayout.EndHorizontal();

        bodyScroll = EditorGUILayout.BeginScrollView(bodyScroll);
        InspectorFor(table).OnInspectorGUI();
        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndVertical();
    }

    void DrawReport()
    {
        if (report == null) return;

        var height = Mathf.Clamp((report.Errors.Count + report.Notes.Count) * 16f + 8f, 40f, 140f);
        reportScroll = EditorGUILayout.BeginScrollView(reportScroll, GUILayout.Height(height));

        foreach (var error in report.Errors) EditorGUILayout.LabelField("✖ " + error, ErrorStyle);
        foreach (var note in report.Notes) EditorGUILayout.LabelField("· " + note, EditorStyles.miniLabel);

        EditorGUILayout.EndScrollView();
    }

    static GUIStyle categoryStyle;

    /// 왼쪽 목록 버튼. `EditorStyles`를 직접 고치면 에디터 전체가 바뀌므로 복사해서 쓴다.
    ///
    /// `fixedHeight`를 0으로 푸는 것이 핵심이다 — `miniButton`은 높이가 18로 고정돼 있어서
    /// 그대로 두면 `GUILayout.Height`가 먹지 않는다.
    static GUIStyle CategoryStyle => categoryStyle ??= new GUIStyle(EditorStyles.miniButton)
    {
        fixedHeight = 0f,
        alignment = TextAnchor.MiddleLeft,
        padding = new RectOffset(10, 6, 0, 0),
        margin = new RectOffset(2, 2, 1, 1),
    };

    static GUIStyle errorStyle;

    static GUIStyle ErrorStyle => errorStyle ??= new GUIStyle(EditorStyles.miniLabel)
    {
        normal = { textColor = new Color(0.9f, 0.35f, 0.3f) },
        wordWrap = true,
    };

    void Run(DataTableAsset only)
    {
        report = DataTableImporter.Import(manager, only);
        LogReport(report);
        Repaint();
    }

    static void LogReport(DataTableImporter.Report result)
    {
        if (result == null) return;

        foreach (var note in result.Notes) CDebug.Log("데이터 표: " + note);

        // 오류는 릴리스에도 남는 통로라 한 줄로 묶어 보낸다. 창에는 전부 따로 보인다.
        if (result.Errors.Count > 0)
            CDebug.LogError("데이터 표 임포트 실패:\n" + string.Join("\n", result.Errors));
    }

    Editor InspectorFor(Object target)
    {
        if (inspectors.TryGetValue(target, out var editor) && editor != null) return editor;

        editor = Editor.CreateEditor(target);
        inspectors[target] = editor;
        return editor;
    }

    /// 프로젝트 안이면 `Assets/...` 상대 경로로 줄인다. 밖이면 절대 경로 그대로 둔다 —
    /// 엑셀을 저장소 밖에 두고 쓰는 경우도 막지 않는다.
    static string ToProjectPath(string absolute)
    {
        var root = System.IO.Path.GetFullPath(Application.dataPath + "/..").Replace('\\', '/');
        var full = absolute.Replace('\\', '/');
        return full.StartsWith(root + "/") ? full.Substring(root.Length + 1) : full;
    }
}
