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
public sealed class UIManager : MonoBehaviour
{
    [Header("프리팹")]
    /// 이 씬이 열 수 있는 화면과 팝업 전부. 타입이 열쇠라서 같은 타입을 두 번 넣을 수 없다.
    [SerializeField] UIView[] prefabs = Array.Empty<UIView>();

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

    /// 지금 맨 위에 있는 화면. 스택이 비었으면 null이다.
    public UIScreen CurrentScreen => screens.Count > 0 ? screens[screens.Count - 1] : null;

    public UIPopup CurrentPopup => popups.Count > 0 ? popups[popups.Count - 1] : null;

    public int ScreenDepth => screens.Count;
    public int PopupDepth => popups.Count;

    void Awake()
    {
        DevHud.EnsureEventSystem();

        foreach (var prefab in prefabs)
        {
            if (prefab == null)
            {
                Debug.LogError($"{name}: 프리팹 목록에 빈 칸이 있다. 지우거나 채워야 한다.", this);
                continue;
            }

            var type = prefab.GetType();
            if (prefabByType.ContainsKey(type))
            {
                Debug.LogError($"{name}: {type.Name} 프리팹이 목록에 둘이다. 타입이 열쇠라 "
                             + "하나만 있을 수 있다.", this);
                continue;
            }
            prefabByType[type] = prefab;
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
        for (var i = screens.Count - 1; i >= 0; i--)
        {
            screens[i].IsOnStack = false;
            screens[i].HideInternal();
            screens[i].SetVisible(false);
        }
        screens.Clear();
        return PushScreen<T>();
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
            Debug.LogError($"{name}: {type.Name} 프리팹이 목록에 없다. UIManager의 프리팹 "
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
