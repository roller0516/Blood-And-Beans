using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// 캐릭터 모델 하나를 어드레서블로 세우고 치우는 일반 C# 객체. 캐릭터 선택 무대의 자리
/// 하나(`CharacterStage`)와 인게임 플레이어(`PlayerVisuals`)가 각각 하나씩 들고 쓴다 —
/// 같은 로드·교체·해제 규칙을 두 자리가 따로 구현하면 한쪽만 고쳐 어긋나기 쉽다.
///
/// **세대 번호로 스폰 순서를 지킨다.** 캐릭터 교체·늦은 참가·씬 재실행마다 이전 로드가
/// 아직 끝나지 않은 채로 새 요청이 올 수 있다. 늦게 끝난 이전 요청이 새 모델을 덮어쓰지
/// 않도록, 완료 시점에 세대가 그대로일 때만 결과를 반영한다 — 다르면 방금 불러온 몫을
/// 바로 반환한다.
public sealed class CharacterModelSpawner
{
    readonly CharacterVisualConfig config;

    int generation;
    GameObject instance;
    AssetReference loadedRef;

    public CharacterModelSpawner(CharacterVisualConfig config) => this.config = config;

    /// 지금 서 있는 모델의 표시 컴포넌트. 없으면 null이다.
    public CharacterModel Model { get; private set; }

    /// 지금 서 있는 모델 루트. 없으면 null이다.
    public GameObject Instance => instance;

    /// 이 종류의 모델을 불러와 세운다. 취소되거나 그사이 새 요청이 오면 이 호출은 아무것도
    /// 바꾸지 않는다. 로드에 실패해도 예외를 던지지 않고 자리를 비운 채 알린다.
    public async UniTask SpawnAsync(CharacterId id, Transform parent, int layer, CancellationToken ct = default)
    {
        var myGeneration = ++generation;
        var reference = config != null ? config.ReferenceFor(id) : null;

        GameObject prefab = null;
        if (reference != null && reference.RuntimeKeyIsValid())
        {
            try
            {
                prefab = await ResourceManager.Instance.LoadAsync<GameObject>(reference, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                CDebug.LogError($"CharacterModelSpawner: '{id}' 모델을 불러오지 못했다. {e.Message}");
                prefab = null;
            }
        }

        // 그사이 다른 요청(재선택·재스폰)이 왔다. 이번 결과는 버리고, 불러온 몫이 있으면 돌려준다.
        if (myGeneration != generation)
        {
            if (prefab != null) ResourceManager.Instance.Release<GameObject>(reference);
            return;
        }

        ClearInternal();

        if (prefab == null || parent == null) return;

        instance = UnityEngine.Object.Instantiate(prefab, parent);
        loadedRef = reference;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        config?.ApplyTransform(id, instance.transform);

        foreach (var child in instance.GetComponentsInChildren<Transform>(true))
            child.gameObject.layer = layer;

        Model = instance.GetComponent<CharacterModel>();
    }

    /// 지금 세운 모델을 지운다. 진행 중이던 로드가 있으면 그 결과도 무시하게 만든다.
    public void Clear()
    {
        generation++;
        ClearInternal();
    }

    void ClearInternal()
    {
        if (instance != null)
        {
            instance.SetActive(false);
            UnityEngine.Object.Destroy(instance);
            instance = null;
        }
        if (loadedRef != null)
        {
            ResourceManager.Instance.Release<GameObject>(loadedRef);
            loadedRef = null;
        }
        Model = null;
    }
}
