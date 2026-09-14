using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// Addressables 로드·해제 관리. 같은 키를 여러 곳에서 불러도 핸들은 하나이고,
/// 불러온 횟수만큼 `Release`해야 실제로 풀린다. 키는 주소·라벨 문자열이나 `AssetReference`다.
public partial class ResourceManager
{
    // 같은 키라도 단일 로드(T)와 묶음 로드(IList<T>)는 서로 다른 핸들이다.
    struct Loaded
    {
        public AsyncOperationHandle Handle;
        public int Count;
    }

    [NonSerialized] Dictionary<(object, Type), Loaded> loaded;

    /// 에셋 하나를 불러온다. 다 쓰면 같은 키로 <see cref="Release{T}"/>를 부른다.
    /// 취소되면 이 호출분은 자동으로 해제된다.
    public UniTask<T> LoadAsync<T>(object key, CancellationToken ct = default)
    {
        var id = (Normalize(key), typeof(T));
        var handle = Acquire(id, () => Addressables.LoadAssetAsync<T>(id.Item1)).Convert<T>();
        return Await(handle, id, ct);
    }

    /// 키(주로 라벨)에 걸린 에셋을 전부 불러온다. 다 쓰면 <see cref="ReleaseAll{T}"/>를 부른다.
    public UniTask<IList<T>> LoadAllAsync<T>(object key, CancellationToken ct = default)
    {
        var id = (Normalize(key), typeof(IList<T>));
        var handle = Acquire(id, () => Addressables.LoadAssetsAsync<T>(id.Item1, null)).Convert<IList<T>>();
        return Await(handle, id, ct);
    }

    public void Release<T>(object key) => Release((Normalize(key), typeof(T)));

    public void ReleaseAll<T>(object key) => Release((Normalize(key), typeof(IList<T>)));

    /// 프리팹을 불러와 생성한다. 파괴는 `Destroy`가 아니라 <see cref="ReleaseInstance"/>로 한다 —
    /// 그래야 프리팹 핸들이 함께 풀린다.
    public UniTask<GameObject> InstantiateAsync(object key, Transform parent = null, CancellationToken ct = default) =>
        Addressables.InstantiateAsync(Normalize(key), parent)
            .ToUniTask(cancellationToken: ct, autoReleaseWhenCanceled: true);

    public void ReleaseInstance(GameObject instance)
    {
        if (!Addressables.ReleaseInstance(instance))
            CDebug.LogError($"{AssetName}: '{instance.name}'은 {nameof(InstantiateAsync)}로 만든 오브젝트가 아니다.", instance);
    }

#if UNITY_EDITOR
    /// 동기 로드. 재생 전 에디터 도구 전용이다 — 런타임은 비동기 API만 쓴다.
    T LoadBlocking<T>(object key)
    {
        var id = (Normalize(key), typeof(T));
        var handle = Acquire(id, () => Addressables.LoadAssetAsync<T>(id.Item1)).Convert<T>();
        var result = handle.WaitForCompletion();
        if (handle.Status != AsyncOperationStatus.Succeeded) Release(id);
        return result;
    }
#endif

    // AssetReference·AssetLabelReference는 인스턴스가 아니라 런타임 키로 묶어야 같은 에셋끼리 핸들을 나눈다.
    static object Normalize(object key) => key is IKeyEvaluator evaluator ? evaluator.RuntimeKey : key;

    AsyncOperationHandle Acquire((object, Type) id, Func<AsyncOperationHandle> load)
    {
        loaded ??= new Dictionary<(object, Type), Loaded>();
        var entry = loaded.TryGetValue(id, out var existing) ? existing : new Loaded { Handle = load() };
        entry.Count++;
        loaded[id] = entry;
        return entry.Handle;
    }

    async UniTask<T> Await<T>(AsyncOperationHandle<T> handle, (object, Type) id, CancellationToken ct)
    {
        try
        {
            return await handle.ToUniTask(cancellationToken: ct);
        }
        catch
        {
            // 취소·실패한 호출분은 호출자가 Release할 수 없으므로 여기서 돌려준다.
            Release(id);
            throw;
        }
    }

    void Release((object, Type) id)
    {
        if (loaded == null || !loaded.TryGetValue(id, out var entry))
        {
            CDebug.LogError($"{AssetName}: 불러오지 않은 '{id.Item1}'({id.Item2.Name})을 해제하려 했다. 해제가 중복됐다.", this);
            return;
        }

        if (--entry.Count > 0)
        {
            loaded[id] = entry;
            return;
        }

        loaded.Remove(id);
        // 플레이·앱 종료 때는 Addressables가 씬 오브젝트의 OnDestroy보다 먼저 내려가 핸들이 이미 무효다.
        if (entry.Handle.IsValid()) Addressables.Release(entry.Handle);
    }
}
