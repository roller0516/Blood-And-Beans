using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// 개발용 콘솔의 껍데기. 탭을 그리고 그룹을 얹고 주기적으로 갱신할 뿐, 어떤 조작이
/// 들어 있는지는 모른다. 실제 내용은 <see cref="DevConsoleGroup"/> 구현들이 가진다.
///
/// 에디터 창이라 씬 배선이 필요 없고, 게임 화면을 가리지 않으며, 빌드에 섞이지 않는다.
/// 접속과 치트를 만지는 유일한 창이다 — 화면을 덮던 런타임 HUD는 지웠다. MPPM 가상
/// 플레이어에는 이 창이 없으므로, 가상 플레이어 쪽 조작은 메인 에디터에서 한다.
///
/// <b>그룹을 더하려면</b> `Editor/DevConsole/Groups/`에 <see cref="DevConsoleGroup"/> 구현을
/// 만들고 아래 <see cref="groups"/>에 한 줄 더한다. 같은 `Tab` 이름이면 그 탭에 얹히고,
/// 새 이름이면 탭이 하나 생긴다. 이 파일의 나머지는 손대지 않는다.
///
/// 생김새는 전부 `DevConsoleWindow.uss`에 있다. 여기에는 배치와 갱신 주기만 둔다.
public class DevConsoleWindow : EditorWindow
{
    const string UssPath = "Assets/Editor/DevConsoleWindow.uss";

    /// 갱신 주기. 남은 시간이 계속 줄지만 매 에디터 틱마다 문자열을 만들 이유는 없다.
    const double RefreshInterval = 0.1;

    /// 배열 순서가 곧 탭 순서이자 탭 안에서의 그룹 순서다.
    readonly DevConsoleGroup[] groups =
    {
        new SessionGroup(),
        new PhaseCheatGroup(),
        new TeamCheatGroup(),
        new CameraGroup(),
        new LookSensitivityGroup(),
        new UIThemeGroup(),
    };

    // 매치 씬은 재생보다 늦게 로드된다. 찾을 때까지만 찾고 찾은 뒤에는 안 찾는다.
    // 두 싱글턴 모두 캐시가 비면 FindAnyObjectByType으로 떨어지므로 그리기 경로에 두지 않는다.
    GamePhase clock;
    MatchSeating seating;

    Label connectionPill;
    VisualElement tabBar, body;
    ScrollView tabStrip;
    Button tabPrev, tabNext;
    readonly List<Button> tabButtons = new();
    readonly List<VisualElement> pages = new();

    double nextRefresh;

    [MenuItem("Blood & Beans/개발 콘솔")]
    static void Open()
    {
        var window = GetWindow<DevConsoleWindow>("개발 콘솔");
        window.minSize = new Vector2(220f, 200f);
    }

    void CreateGUI()
    {
        var root = rootVisualElement;
        var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(UssPath);
        if (sheet == null)
            Debug.LogError($"{UssPath}를 찾을 수 없다. 창은 뜨지만 스타일이 빠진다.");
        else
            root.styleSheets.Add(sheet);

        root.AddToClassList("root");

        // 상단 바와 탭 줄은 고정한다. 접속 상태와 탭은 본문을 아무리 스크롤해도 보여야 한다.
        BuildTopBar(root);
        BuildTabBar(root);

        // 한 번에 한 탭만 보이지만, 그 한 탭이 길어질 수 있어 세로 스크롤은 남긴다.
        var scroll = new ScrollView(ScrollViewMode.Vertical);
        scroll.AddToClassList("scroll");
        root.Add(scroll);
        body = scroll;

        // 같은 탭 이름을 쓰는 그룹은 한 페이지에 차례로 쌓인다.
        var byTab = new Dictionary<string, VisualElement>();
        foreach (var group in groups)
        {
            if (!byTab.TryGetValue(group.Tab, out var page))
            {
                page = new VisualElement();
                byTab[group.Tab] = page;
                AddTab(group.Tab, page);
            }
            group.Attach(page);
        }

        SelectTab(0);
        Refresh();
    }

    void BuildTopBar(VisualElement parent)
    {
        var bar = new VisualElement();
        bar.AddToClassList("topbar");

        var brand = new Label("BLOOD & BEANS");
        brand.AddToClassList("brand");
        bar.Add(brand);

        connectionPill = new Label("OFFLINE");
        connectionPill.AddToClassList("pill");
        bar.Add(connectionPill);

        parent.Add(bar);
    }

    // ── 탭 ────────────────────────────────────────────────────────

    void BuildTabBar(VisualElement parent)
    {
        tabBar = new VisualElement();
        tabBar.AddToClassList("tabbar");

        tabPrev = new Button(() => StepTabs(-1)) { text = "‹" };
        tabPrev.AddToClassList("arrow");
        tabBar.Add(tabPrev);

        // 탭이 창보다 넓어지면 가로로 밀어서 본다. 스크롤바는 화살표로 대신하므로 감춘다.
        tabStrip = new ScrollView(ScrollViewMode.Horizontal);
        tabStrip.AddToClassList("tabstrip");
        tabStrip.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        tabStrip.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        tabBar.Add(tabStrip);

        tabNext = new Button(() => StepTabs(1)) { text = "›" };
        tabNext.AddToClassList("arrow");
        tabBar.Add(tabNext);

        // 창 크기가 바뀔 때마다 넘치는지 다시 잰다. 레이아웃이 끝난 뒤라야 폭이 확정된다.
        tabStrip.RegisterCallback<GeometryChangedEvent>(_ => RefreshTabOverflow());
        tabBar.RegisterCallback<GeometryChangedEvent>(_ => RefreshTabOverflow());

        parent.Add(tabBar);
    }

    void AddTab(string label, VisualElement page)
    {
        var index = pages.Count;

        var button = new Button(() => SelectTab(index)) { text = label };
        button.AddToClassList("tab");
        tabStrip.Add(button);
        tabButtons.Add(button);

        page.AddToClassList("page");
        pages.Add(page);
        body.Add(page);
    }

    void SelectTab(int index)
    {
        if (index < 0 || index >= pages.Count) return;

        for (var i = 0; i < pages.Count; i++)
        {
            pages[i].style.display = i == index ? DisplayStyle.Flex : DisplayStyle.None;
            tabButtons[i].EnableInClassList("tab--on", i == index);
        }

        // 고른 탭이 잘려 있으면 보이는 데까지 끌어온다.
        tabStrip.ScrollTo(tabButtons[index]);
    }

    /// 넘칠 때만 화살표를 띄운다. 다 보이는데 화살표가 남아 있으면 누를 데가 없어 헷갈린다.
    ///
    /// 내용 폭과 뷰포트 폭을 직접 재지 않는다. 가로 ScrollView의 content container는 뷰포트에
    /// 맞춰 늘어나기도 해서 두 값 비교가 넘침을 반영하지 못한다. 스크롤러의 `highValue`는
    /// ScrollView가 넘친 만큼만 채우는 값이라 그 자체가 넘침 여부다.
    void RefreshTabOverflow()
    {
        if (tabStrip == null) return;

        var scroller = tabStrip.horizontalScroller;
        var overflow = scroller != null && scroller.highValue > 1f;

        var display = overflow ? DisplayStyle.Flex : DisplayStyle.None;
        tabPrev.style.display = display;
        tabNext.style.display = display;
    }

    /// 지금 잘려 있는 쪽의 첫 탭을 화면 안으로 끌어온다 — 누를 때마다 다음 탭이 나온다.
    void StepTabs(int direction)
    {
        var viewport = tabStrip.contentViewport.worldBound;

        if (direction > 0)
        {
            foreach (var tab in tabButtons)
                if (tab.worldBound.xMax > viewport.xMax + 1f) { tabStrip.ScrollTo(tab); return; }
        }
        else
        {
            for (var i = tabButtons.Count - 1; i >= 0; i--)
                if (tabButtons[i].worldBound.xMin < viewport.xMin - 1f)
                {
                    tabStrip.ScrollTo(tabButtons[i]);
                    return;
                }
        }
    }

    // ── 갱신 ──────────────────────────────────────────────────────

    void Update()
    {
        if (!EditorApplication.isPlaying)
        {
            // 재생을 벗어나면 파괴된 오브젝트를 붙들고 있지 않는다.
            clock = null;
            seating = null;
        }
        else
        {
            if (clock == null)
            {
                var director = MatchDirector.Instance;
                clock = director != null ? director.Phase : null;
            }
            if (seating == null) seating = GameManager.Seating;
        }

        if (EditorApplication.timeSinceStartup < nextRefresh) return;
        nextRefresh = EditorApplication.timeSinceStartup + RefreshInterval;
        Refresh();
    }

    /// 탭 줄과 본문은 재생 여부와 상관없이 항상 띄운다. 재생 전에 숨기면 "시작" 탭에서
    /// 매치를 띄우기도 전에 다른 탭이 사라져, 창이 고장 난 것처럼 보인다. 조작 가능 여부는
    /// 각 그룹이 자기 버튼을 잠가서 나타낸다.
    void Refresh()
    {
        if (tabBar == null) return;   // CreateGUI 전에 불릴 수 있다

        var playing = EditorApplication.isPlaying;
        var manager = playing ? NetworkManager.Singleton : null;
        var listening = manager != null && manager.IsListening;
        var isServer = manager != null && manager.IsServer;

        connectionPill.text = !listening ? "OFFLINE"
            : manager.IsHost ? "HOST" : isServer ? "SERVER" : "CLIENT";
        connectionPill.EnableInClassList("pill--live", listening);
        connectionPill.EnableInClassList("pill--off", !listening);

        // 그룹이 늘어도 이 함수는 그대로다.
        var state = new DevConsoleState(playing, listening, isServer, clock, seating);
        foreach (var group in groups) group.Refresh(state);
    }
}
