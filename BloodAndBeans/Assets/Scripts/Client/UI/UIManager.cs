using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

/// 이 씬의 UI 조립 루트. 화면과 팝업 프리팹을 만들고, 스택으로 흐름을 관리하고,
/// 겹침 순서를 배분한다.
///
/// 화면 스택과 팝업 스택을 나눈 이유는 수명이 다르기 때문이다. 화면은 한 번에 하나만
/// 보이고 뒤로 가기로 되돌아간다. 팝업은 화면을 지우지 않고 그 위에 쌓인다.
///
/// 프리팹은 타입으로 찾는다. Inspector의 목록에는 Addressables 참조만 두어, 이 오브젝트가
/// 프리팹(과 프리팹이 품은 스프라이트)을 붙잡지 않는다. 뷰는 처음 열 때 불러오고
/// <see cref="Unload{T}"/>·<see cref="UnloadUnused"/>로 놓는다.
///
/// **`GameManager` 프리팹의 자식이라 게임매니저와 함께 만들어지고 함께 살아남는다.**
/// `DontDestroyOnLoad`를 스스로 부르지 않는 이유가 이것이다 — 자식에게 부르면 Unity가
/// 경고만 내고 아무 일도 하지 않는다. 영속은 루트인 게임매니저가 준다.
///
/// 그래서 씬에 UIManager를 놓을 필요도, 조립 지점이 만들 필요도 없다. 어느 씬에서 시작하든
/// 첫 프레임부터 `Instance`가 서 있다.
///
/// 영속이라는 것은 **아무도 비워 주지 않는다**는 뜻이기도 하다. 씬을 넘길 때 앞 씬의 화면이
/// 스택에 깔린 채 남으면 계속 쌓이므로, 넘기는 쪽이 `ClearScreens`·`PopAllPopups`·`UnloadUnused`로 자기
/// 것을 치운다.
public sealed class UIManager : MonoSingleton<UIManager>
{
    [Serializable]
    public struct ViewEntry
    {
        [Tooltip("프리팹 루트의 UIView 타입. 프리팹을 넣으면 에디터가 채운다.")]
        public string type;
        public AssetReferenceGameObject prefab;
    }

    [Header("프리팹")]
    /// 화면과 팝업. 수명은 목록이 아니라 불러 두고 놓는 흐름이 정한다.
    [SerializeField] ViewEntry[] views = Array.Empty<ViewEntry>();

    [Header("겹침 순서")]
    /// 화면과 팝업이 쓰는 sortingOrder 구간. 화면 위에 팝업이 오도록 간격을 벌려 둔다.
    [SerializeField] int screenBaseOrder = 100;
    [SerializeField] int popupBaseOrder = 500;
    [SerializeField] int orderStep = 10;

    readonly Dictionary<Type, AssetReferenceGameObject> prefabByType = new();

    /// 불러 둔 뷰는 감췄다가 다시 쓴다. 화면을 오갈 때마다 새로 만들면 GC와 함께
    /// 직렬화 참조를 다시 잇는 비용이 매번 든다.
    readonly Dictionary<Type, UIView> instanceByType = new();

    /// 불러오는 중인 뷰. 같은 뷰를 여러 곳이 동시에 기다려도 하나만 만든다.
    /// `AsyncLazy`인 이유는 끝나기 전에 여러 번 await할 수 있어야 해서다 — `Preserve`는 끝난 뒤에만 된다.
    readonly Dictionary<Type, AsyncLazy> loadingByType = new();

    /// 불러오는 도중에 놓으라는 요청을 받은 뷰. 다 만들어지는 즉시 돌려준다.
    readonly HashSet<Type> unloadRequested = new();

    /// 불러오기에 실패한 뷰. 오류는 한 번만 알리고, 놓기 전까지 다시 시도하지 않는다.
    readonly HashSet<Type> failedTypes = new();

    /// 딕셔너리를 순회하면서 지우기 위한 임시 목록. 매번 새로 만들지 않는다.
    readonly List<Type> scratch = new();

    readonly List<UIScreen> screens = new();
    readonly List<UIPopup> popups = new();

    /// 지금 맨 위에 있는 화면. 스택이 비었으면 null이다.
    public UIScreen CurrentScreen => screens.Count > 0 ? screens[screens.Count - 1] : null;

    public UIPopup CurrentPopup => popups.Count > 0 ? popups[popups.Count - 1] : null;

    /// 지금 떠 있는 UI가 캐릭터 조작을 막고 있는가. 입력을 읽는 쪽이 본다
    /// (`PlayerInputRouter`).
    public bool PlayerInputBlocked { get; private set; }

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

        if (FindAnyObjectByType<EventSystem>() == null)
        {
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            eventSystem.transform.SetParent(this.transform);
        }

        Register();
    }

    /// 목록을 타입 색인에 넣는다. 화면과 팝업이 같은 색인을 쓰는 이유는 여는 쪽이
    /// 타입으로만 찾기 때문이다.
    void Register()
    {
        foreach (var entry in views)
        {
            var type = string.IsNullOrEmpty(entry.type) ? null : Type.GetType(entry.type);
            if (type == null || entry.prefab == null || !entry.prefab.RuntimeKeyIsValid())
            {
                CDebug.LogError($"{name}: 프리팹 목록에 빈 칸이나 찾을 수 없는 타입('{entry.type}')이 있다.", this);
                continue;
            }

            if (prefabByType.ContainsKey(type))
            {
                CDebug.LogError($"{name}: {type.Name} 프리팹이 목록에 둘이다. 타입이 열쇠라 "
                              + "하나만 있을 수 있다.", this);
                continue;
            }
            prefabByType[type] = entry.prefab;
        }
    }

#if UNITY_EDITOR
    // 프리팹을 넣으면 타입 이름을 채운다. 사람이 문자열을 적지 않게 한다.
    void OnValidate()
    {
        for (var i = 0; i < views.Length; i++)
        {
            var asset = views[i].prefab?.editorAsset as GameObject;
            var view = asset != null ? asset.GetComponent<UIView>() : null;
            var type = view != null ? view.GetType() : null;
            views[i].type = type != null ? $"{type.FullName}, {type.Assembly.GetName().Name}" : string.Empty;
        }
    }
#endif

    /// 이미 만들어진 뷰를 찾는다. 만들지는 않는다 — 아직 그 화면이 뜨기 전이면 false다.
    public bool TryGet<T>(out T view) where T : UIView
    {
        var found = instanceByType.TryGetValue(typeof(T), out var cached) && cached != null;
        view = found ? (T)cached : null;
        return found;
    }

    /// 이 뷰를 불러오지 못했는가. 여는 API의 null이 "불러오는 중"인지 "실패"인지 가를 때 본다.
    public bool FailedToLoad<T>() where T : UIView => failedTypes.Contains(typeof(T));

    // --- 적재 ---
    // 여는 API는 불러 둔 뷰만 연다. 아직이면 불러오기를 시작하고 null을 돌려주므로, 매 프레임 상태를
    // 맞추는 호출부는 다음 프레임에 다시 열면 된다. 한 번 여는 호출부는 LoadAsync를 기다린 뒤 연다.

    /// 뷰를 비동기로 불러 만들어 둔다. 스택에는 올리지 않는다. 호출자가 취소해도 만드는 중인 뷰는 끝까지 만들어진다.
    public UniTask LoadAsync<T>(CancellationToken ct = default) where T : UIView =>
        LoadAsync(typeof(T)).AttachExternalCancellation(ct);

    UniTask LoadAsync(Type type)
    {
        unloadRequested.Remove(type);
        if (instanceByType.ContainsKey(type)) return UniTask.CompletedTask;
        if (!loadingByType.TryGetValue(type, out var loading))
        {
            loading = InstantiateAsync(type).ToAsyncLazy();
            // 즉시 끝났으면(목록에 없음 등) 끝난 자리의 finally가 이미 지나갔다. 등록하지 않는다.
            if (loading.Task.Status == UniTaskStatus.Pending) loadingByType[type] = loading;
        }
        return loading.Task;
    }

    async UniTask InstantiateAsync(Type type)
    {
        try
        {
            if (!prefabByType.TryGetValue(type, out var prefab))
            {
                failedTypes.Add(type);
                CDebug.LogError($"{name}: {type.Name} 프리팹이 목록에 없다. UIManager의 프리팹 "
                              + "목록에 이어 두지 않은 화면은 열 수 없다.", this);
                return;
            }

            GameObject instance;
            try
            {
                instance = await ResourceManager.Instance.InstantiateAsync(prefab, transform, this.GetCancellationTokenOnDestroy());
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                // 매 프레임 다시 여는 호출부가 같은 실패를 되풀이하지 않게 막는다.
                failedTypes.Add(type);
                CDebug.LogError($"{name}: {type.Name} 프리팹을 불러오지 못했다. {e.Message}", this);
                return;
            }

            // 만들어진 순간 한 번 찾는다. 프리팹 루트에 그 타입이 있어야 한다.
            var view = instance.GetComponent(type) as UIView;
            if (view == null)
            {
                failedTypes.Add(type);
                CDebug.LogError($"{name}: {type.Name} 목록의 프리팹 루트에 {type.Name}이 없다.", this);
                ResourceManager.Instance.ReleaseInstance(instance);
                return;
            }

            instance.name = type.Name;
            view.SetVisible(false);          // 보이기는 스택에 올라갈 때만
            if (unloadRequested.Remove(type))
            {
                ResourceManager.Instance.ReleaseInstance(instance);
                return;
            }
            instanceByType[type] = view;
        }
        finally
        {
            loadingByType.Remove(type);
        }
    }

    /// 뷰를 파괴하고 프리팹을 놓는다. 스택에 올라가 있으면 먼저 내린다. 다시 열면 다시 불러온다.
    public void Unload<T>() where T : UIView => Unload(typeof(T));

    /// 스택에 없는 뷰를 모두 놓는다. 씬을 떠나는 흐름이 자기 화면을 치운 뒤 부른다.
    /// 불러오는 중인 뷰는 건드리지 않는다 — 다음 씬이 막 열려는 것일 수 있다.
    public void UnloadUnused()
    {
        scratch.Clear();
        foreach (var pair in instanceByType)
            if (pair.Value == null || !pair.Value.IsOnStack) scratch.Add(pair.Key);

        for (var i = 0; i < scratch.Count; i++) Unload(scratch[i]);
    }

    void Unload(Type type)
    {
        failedTypes.Remove(type);
        if (loadingByType.ContainsKey(type))
        {
            unloadRequested.Add(type);
            return;
        }
        if (!instanceByType.Remove(type, out var view) || view == null) return;

        if (view.IsOnStack)
        {
            if (view is UIScreen screen) screens.Remove(screen);
            if (view is UIPopup popup) popups.Remove(popup);
            view.IsOnStack = false;
            view.HideInternal();
            ApplyInputGates();
        }
        view.OnUnload();
        ResourceManager.Instance.ReleaseInstance(view.gameObject);
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
        ApplyInputGates();
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
        ApplyInputGates();
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
        ApplyInputGates();
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
        ApplyInputGates();
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
        ApplyInputGates();
    }

    public void PopAllPopups()
    {
        while (popups.Count > 0) PopPopup();
    }

    // --- 조립 ---

    /// 불러 둔 뷰를 돌려준다. 아직이면 불러오기를 시작하고 null이다.
    T Resolve<T>() where T : UIView
    {
        var type = typeof(T);
        if (instanceByType.TryGetValue(type, out var cached) && cached != null) return (T)cached;
        if (!failedTypes.Contains(type)) LoadAsync(type).Forget();
        return null;
    }

    /// 커서와 이동 차단은 지금 떠 있는 UI가 정한다. 팝업이 하나라도 원하면 그렇게 되고,
    /// 없으면 맨 위 화면이 정한다 (`UIView.WantsCursor`, `UIView.BlocksPlayerInput`).
    ///
    /// 스택이 바뀌는 자리마다 한 번씩 부른다. 뷰가 스스로 켜고 끄면 팝업을 닫는 순간
    /// 밑에 깔린 화면이 무엇을 원하는지 모른 채 커서를 지워 버린다.
    ///
    /// 화면이 없을 때의 기본값이 둘로 갈린다. 커서는 보여 준다 — 화면이 없는 순간에
    /// 커서까지 잠기면 사용자가 아무것도 할 수 없다. 조작은 막지 않는다 — 막는 것은
    /// 창이 요구할 때만이고, 요구한 창이 없으면 막을 이유도 없다.
    /// 스택은 그대로인데 뷰가 원하는 커서가 바뀌었을 때 부른다 (HUD 조합식 패널).
    public void RefreshInputGates() => ApplyInputGates();

    void ApplyInputGates()
    {
        var wants = false;
        var blocks = false;
        for (var i = 0; i < popups.Count; i++)
        {
            wants |= popups[i].WantsCursor;
            blocks |= popups[i].BlocksPlayerInput;
        }
        if (!wants) wants = CurrentScreen == null || CurrentScreen.WantsCursor;
        if (!blocks) blocks = CurrentScreen != null && CurrentScreen.BlocksPlayerInput;

        PlayerInputBlocked = blocks;
        Cursor.visible = wants;
        Cursor.lockState = wants ? CursorLockMode.None : CursorLockMode.Locked;
    }

    void ShowAt(UIView view, int order)
    {
        view.SetSortingOrder(order);
        view.SetVisible(true);
        view.ShowInternal();
    }
}
