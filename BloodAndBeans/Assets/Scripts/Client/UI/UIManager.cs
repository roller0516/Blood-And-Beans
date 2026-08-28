using System;
using System.Collections.Generic;
using UnityEngine;

/// 이 씬의 UI 조립 루트. 화면과 팝업 프리팹을 만들고, 스택으로 흐름을 관리하고,
/// 겹침 순서를 배분한다.
///
/// 화면 스택과 팝업 스택을 나눈 이유는 수명이 다르기 때문이다. 화면은 한 번에 하나만
/// 보이고 뒤로 가기로 되돌아간다. 팝업은 화면을 지우지 않고 그 위에 쌓인다.
///
/// 프리팹은 타입으로 찾는다. 문자열 경로나 `Resources.Load`를 쓰지 않는 이유는, 이름이
/// 바뀌면 컴파일이 아니라 런타임에 터지기 때문이다. Inspector에 이어 둔 것만 열 수 있고
/// 이어 두지 않은 것을 열려고 하면 즉시 오류로 드러난다.
///
/// **`GameManager` 프리팹의 자식이라 게임매니저와 함께 만들어지고 함께 살아남는다.**
/// `DontDestroyOnLoad`를 스스로 부르지 않는 이유가 이것이다 — 자식에게 부르면 Unity가
/// 경고만 내고 아무 일도 하지 않는다. 영속은 루트인 게임매니저가 준다.
///
/// 그래서 씬에 UIManager를 놓을 필요도, 조립 지점이 만들 필요도 없다. 어느 씬에서 시작하든
/// 첫 프레임부터 `Instance`가 서 있다.
///
/// 영속이라는 것은 **아무도 비워 주지 않는다**는 뜻이기도 하다. 씬을 넘길 때 앞 씬의 화면이
/// 스택에 깔린 채 남으면 계속 쌓이므로, 넘기는 쪽이 `ClearScreens`·`UnloadPopups`로 자기
/// 것을 치운다.
public sealed class UIManager : MonoSingleton<UIManager>
{
    [Header("프리팹")]
    /// 화면. 뒤로 가기로 되돌아올 수 있어야 하므로 한 번 만들면 계속 들고 있는다.
    [SerializeField]
    private UIScreen[] screenPrefabs;

    /// 팝업. 화면과 목록을 나눈 이유는 수명이 다르기 때문이다 — 팝업은 여닫는 빈도가 높고
    /// 판이 바뀌면 들고 있을 이유가 없다. 언제 만들고 언제 놓을지를 흐름이 정할 수 있어야
    /// 해서 목록부터 갈라 둔다 (`Preload`, `UnloadPopups`).
    [SerializeField] private UIPopup[] popupPrefabs;

    [Header("겹침 순서")]
    /// 화면과 팝업이 쓰는 sortingOrder 구간. 화면 위에 팝업이 오도록 간격을 벌려 둔다.
    [SerializeField] int screenBaseOrder = 100;
    [SerializeField] int popupBaseOrder = 500;
    [SerializeField] int orderStep = 10;

    readonly Dictionary<Type, UIView> prefabByType = new();

    /// 한 번 만든 뷰는 감췄다가 다시 쓴다. 화면을 오갈 때마다 새로 만들면 GC와 함께
    /// 직렬화 참조를 다시 잇는 비용이 매번 든다.
    readonly Dictionary<Type, UIView> instanceByType = new();

    readonly List<UIScreen> screens = new();
    readonly List<UIPopup> popups = new();

    /// 딕셔너리를 순회하면서 지우기 위한 임시 목록. 매번 새로 만들지 않는다.
    readonly List<Type> scratch = new();

    /// 지금 맨 위에 있는 화면. 스택이 비었으면 null이다.
    public UIScreen CurrentScreen => screens.Count > 0 ? screens[screens.Count - 1] : null;

    public UIPopup CurrentPopup => popups.Count > 0 ? popups[popups.Count - 1] : null;

    public int ScreenDepth => screens.Count;
    public int PopupDepth => popups.Count;

    /// 싱글턴 등록과 중복 파괴는 기반 클래스가 한다. `base.Awake()`를 빠뜨리면 `Instance`가
    /// 채워지지 않아 조회하는 쪽이 전부 null을 받는다 (CS0114 경고의 내용).
    protected override void Awake()
    {
        base.Awake();

        // 중복이면 기반 클래스가 이 오브젝트를 파괴하기로 했다. 사라질 오브젝트가 프리팹
        // 색인을 채우고 EventSystem까지 만들면 그것이 그대로 쓰레기가 된다.
        if (Instance != this) return;

        DevHud.EnsureEventSystem();

        Register(screenPrefabs);
        Register(popupPrefabs);
    }

    /// 목록 하나를 타입 색인에 넣는다. 화면과 팝업이 같은 색인을 쓰는 이유는 여는 쪽이
    /// 타입으로만 찾기 때문이다 — 나뉘어 있는 것은 목록과 수명이지 조회가 아니다.
    void Register(UIView[] source)
    {
        if (source == null) return;

        foreach (var prefab in source)
        {
            if (prefab == null)
            {
                CDebug.LogError($"{name}: 프리팹 목록에 빈 칸이 있다. 지우거나 채워야 한다.", this);
                continue;
            }

            var type = prefab.GetType();
            if (prefabByType.ContainsKey(type))
            {
                CDebug.LogError($"{name}: {type.Name} 프리팹이 목록에 둘이다. 타입이 열쇠라 "
                              + "하나만 있을 수 있다.", this);
                continue;
            }
            prefabByType[type] = prefab;
        }
    }

    // --- 적재 시점 ---

    /// 열기 전에 미리 만들어 둔다. 스택에는 올리지 않는다.
    ///
    /// 만드는 비용을 원하는 시점으로 옮기는 것이 전부다. `BoxLootPopup`처럼 밤 한가운데
    /// 처음 열리는 팝업은 그 순간 인스턴스를 만드느라 한 번 끊긴다.
    public void Preload<T>() where T : UIView => Resolve<T>();

    /// 캐시해 둔 팝업 인스턴스를 파괴한다. 다시 열면 새로 만든다.
    ///
    /// 화면은 여기서 놓지 않는다. 뒤로 가기로 돌아올 수 있어야 하고, 어차피 씬과 함께
    /// 죽는다. 판이 끝났는데도 팝업을 들고 있을 이유는 없다.
    public void UnloadPopups()
    {
        PopAllPopups();

        scratch.Clear();
        foreach (var pair in instanceByType)
            if (pair.Value is UIPopup) scratch.Add(pair.Key);

        for (var i = 0; i < scratch.Count; i++)
        {
            var view = instanceByType[scratch[i]];
            instanceByType.Remove(scratch[i]);
            if (view != null) Destroy(view.gameObject);
        }
    }

    // --- 화면 ---

    /// 지금 화면을 덮고 새 화면을 올린다. 밑의 화면은 스택에 남아 있다가 `PopScreen`으로 돌아온다.
    public T PushScreen<T>() where T : UIScreen
    {
        var view = Resolve<T>();
        if (view == null) return null;

        // 화면은 전체를 덮으므로 밑에 깔린 것은 그리지 않는다. 팝업과 다른 점이다.
        var below = CurrentScreen;
        if (below != null)
        {
            below.HideInternal();
            below.SetVisible(false);
        }

        screens.Add(view);
        view.IsOnStack = true;
        ShowAt(view, screenBaseOrder + (screens.Count - 1) * orderStep);
        return view;
    }

    /// 맨 위 화면을 내리고 그 밑을 다시 보여 준다. 마지막 하나는 내리지 않는다 —
    /// 화면이 하나도 없는 상태는 사용자에게 검은 화면이고, 그건 흐름 오류다.
    public void PopScreen()
    {
        if (screens.Count <= 1) return;

        var top = screens[screens.Count - 1];
        screens.RemoveAt(screens.Count - 1);
        top.IsOnStack = false;
        top.HideInternal();
        top.SetVisible(false);

        var below = CurrentScreen;
        if (below != null)
        {
            below.SetVisible(true);
            below.ShowInternal();
        }
    }

    /// 스택을 비우고 이 화면 하나만 남긴다. 로비에서 매치로 넘어가듯 되돌아갈 이유가
    /// 없는 전환에 쓴다.
    public T ReplaceScreen<T>() where T : UIScreen
    {
        ClearScreens();
        return PushScreen<T>();
    }

    /// 화면 스택을 통째로 비운다. 인스턴스는 캐시에 남으므로 다시 열 때 새로 만들지 않는다.
    ///
    /// 이 오브젝트는 씬과 함께 죽지 않는다. 씬을 넘기는 쪽이 자기 화면을 치우지 않으면
    /// 앞 씬의 화면이 계속 스택 밑에 깔린 채 남는다.
    public void ClearScreens()
    {
        for (var i = screens.Count - 1; i >= 0; i--)
        {
            screens[i].IsOnStack = false;
            screens[i].HideInternal();
            screens[i].SetVisible(false);
        }
        screens.Clear();
    }

    // --- 팝업 ---

    /// 화면 위에 겹쳐 띄운다. 밑의 화면은 그대로 보인다.
    public T PushPopup<T>() where T : UIPopup
    {
        var view = Resolve<T>();
        if (view == null) return null;

        popups.Add(view);
        view.IsOnStack = true;
        ShowAt(view, popupBaseOrder + (popups.Count - 1) * orderStep);
        return view;
    }

    public void PopPopup()
    {
        if (popups.Count == 0) return;

        var top = popups[popups.Count - 1];
        popups.RemoveAt(popups.Count - 1);
        top.IsOnStack = false;
        top.HideInternal();
        top.SetVisible(false);
    }

    public void PopAllPopups()
    {
        while (popups.Count > 0) PopPopup();
    }

    // --- 조립 ---

    T Resolve<T>() where T : UIView
    {
        var type = typeof(T);
        if (instanceByType.TryGetValue(type, out var cached)) return (T)cached;

        if (!prefabByType.TryGetValue(type, out var prefab))
        {
            CDebug.LogError($"{name}: {type.Name} 프리팹이 목록에 없다. UIManager의 프리팹 "
                          + "목록에 이어 두지 않은 화면은 열 수 없다.", this);
            return null;
        }

        var instance = Instantiate(prefab, transform);
        instance.name = type.Name;
        instance.SetVisible(false);          // 보이기는 스택에 올라갈 때만
        instanceByType[type] = instance;
        return (T)instance;
    }

    void ShowAt(UIView view, int order)
    {
        view.SetSortingOrder(order);
        view.SetVisible(true);
        view.ShowInternal();
    }
}
