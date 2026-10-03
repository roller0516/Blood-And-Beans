using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

/// `IItemHolder` 한 자리(손 · 조리대 · 설비 · 재료 칸)의 슬롯을 어드레서블 프리팹으로
/// 세우고 치우는 일반 C# 객체. 시설용 `ItemDisplay`와 플레이어의 `PlayerVisuals`가 손
/// 슬롯 표시에 이 하나를 같이 쓴다 — 자리 표시 규칙(프리팹 교체 판단 · 탄 것 재질 ·
/// 강조 크기)이 두 곳에 따로 있으면 한쪽만 고쳐 어긋난다.
///
/// **표현만 한다.** 무엇이 어디 있는지는 `IItemHolder`가 이미 복제된 값으로 답하고,
/// 이 클래스는 그 값이 바뀌었을 때(`Refresh`)만 다시 세운다.
///
/// 칸마다 세대 번호를 둔다. 재료를 빠르게 옮기면 이전 로드가 끝나기 전에 다음 내용으로
/// 또 바뀔 수 있는데, 늦게 끝난 이전 로드가 방금 놓인 새 아이템을 덮어쓰면 안 된다.
public sealed class ItemSlotPresenter
{
    /// 다음에 F가 집을 칸의 렌더링 레이어. `OutlineFeature`가 초록 테두리로 그린다.
    /// `TargetOutline.RenderingLayerBit`(8)의 바로 다음 비트다.
    public const uint NextPickBit = 1u << 9;

    static readonly List<Renderer> Renderers = new();

    readonly ItemVisualConfig config;
    readonly float highlightScale;
    readonly Vector3 highlightOffset;

    // 칸 배열은 `AddSlot`으로 늘어난다.
    Transform[] anchors;
    GameObject[] standing;
    AssetReference[] sources;
    AssetReference[] dishSources;
    bool[] burnt;
    bool[] dirty;
    bool[] toasted;
    int[] generation;

    IItemHolder holder;

    public ItemSlotPresenter(ItemVisualConfig config, Transform[] anchors, float highlightScale, Vector3 highlightOffset)
    {
        this.config = config;
        this.anchors = anchors ?? Array.Empty<Transform>();
        this.highlightScale = highlightScale;
        this.highlightOffset = highlightOffset;

        var count = this.anchors.Length;
        standing = new GameObject[count];
        sources = new AssetReference[count];
        dishSources = new AssetReference[count];
        burnt = new bool[count];
        dirty = new bool[count];
        toasted = new bool[count];
        generation = new int[count];
    }

    /// 칸을 하나 늘린다. 개수 제한 없는 자리가 내용이 늘 때 부른다 (`ItemDisplay`).
    public void AddSlot(Transform anchor)
    {
        var count = anchors.Length + 1;
        Array.Resize(ref anchors, count);
        Array.Resize(ref standing, count);
        Array.Resize(ref sources, count);
        Array.Resize(ref dishSources, count);
        Array.Resize(ref burnt, count);
        Array.Resize(ref dirty, count);
        Array.Resize(ref toasted, count);
        Array.Resize(ref generation, count);
        anchors[count - 1] = anchor;
    }

    /// 자리 주인을 이어 붙이고 즉시 한 번 그린다. 주인이 바뀌는 일은 없으므로(자리는
    /// 자기 `IItemHolder`를 평생 하나만 본다) 여러 번 부를 필요가 없다.
    public void Bind(IItemHolder holder)
    {
        this.holder = holder;
        Refresh();
    }

    /// 자리 하나하나를 복제된 값과 맞춘다. 값이 바뀔 때만 불린다 (`IItemHolder.ContentsChanged`).
    public void Refresh()
    {
        if (holder == null || config == null) return;

        var highlight = holder.HighlightSlot;

        for (var slot = 0; slot < anchors.Length; slot++)
        {
            var anchor = anchors[slot];
            if (anchor == null) continue;

            var view = slot < holder.SlotCount ? holder.SlotAt(slot) : CarryView.Nothing;
            var reference = config.ReferenceFor(view);
            var dish = config.DishFor(view);
            var baked = ItemVisualConfig.IsBaked(view);

            if (!SameKey(reference, sources[slot]) || !SameKey(dish, dishSources[slot])
                || view.Burnt != burnt[slot] || view.Dirty != dirty[slot] || baked != toasted[slot])
                RebuildSlotAsync(slot, reference, dish, !view.DishIsPlate, view.Burnt, view.Dirty, baked,
                                 anchor, anchor.gameObject.layer).Forget();

            ApplyHighlight(slot, slot == highlight);
        }
    }

    static bool SameKey(AssetReference a, AssetReference b)
    {
        var aValid = a != null && a.RuntimeKeyIsValid();
        var bValid = b != null && b.RuntimeKeyIsValid();
        if (!aValid && !bValid) return true;
        if (aValid != bValid) return false;
        return Equals(a.RuntimeKey, b.RuntimeKey);
    }

    async UniTaskVoid RebuildSlotAsync(int slot, AssetReference reference, AssetReference dish, bool inside,
                                       bool isBurnt, bool isDirty, bool isToasted, Transform anchor, int layer)
    {
        var myGeneration = ++generation[slot];

        GameObject prefab = null, dishPrefab = null;
        try
        {
            prefab = await LoadAsync(slot, reference);
            dishPrefab = await LoadAsync(slot, dish);
        }
        catch (OperationCanceledException)
        {
            if (prefab != null) ResourceManager.Instance.Release<GameObject>(reference);
            return;
        }

        // 그사이 이 칸의 내용이 다시 바뀌었다. 이번 결과는 버린다.
        if (myGeneration != generation[slot])
        {
            if (prefab != null) ResourceManager.Instance.Release<GameObject>(reference);
            if (dishPrefab != null) ResourceManager.Instance.Release<GameObject>(dish);
            return;
        }

        ClearSlot(slot);
        sources[slot] = prefab != null ? reference : null;
        dishSources[slot] = dishPrefab != null ? dish : null;
        burnt[slot] = isBurnt;
        dirty[slot] = isDirty;
        toasted[slot] = isToasted;

        if (prefab == null || anchor == null) return;

        var made = Place(prefab, anchor, layer);
        Restate(made, isDirty, isBurnt, isToasted);

        // 그릇을 먼저 세우고 내용물을 얹거나 담는다. 강조는 그릇째 움직인다.
        if (dishPrefab != null)
        {
            var vessel = Place(dishPrefab, anchor, layer);
            Restate(vessel, isDirty, isBurnt, isToasted);
            Seat(made.transform, vessel.transform, inside, config.CupFill);
            made = vessel;
        }

        standing[slot] = made;

        // 로드가 끝나는 사이 강조 칸이 바뀌었을 수 있다. holder는 살아 있는 참조라 지금
        // 값을 다시 물으면 된다.
        ApplyHighlight(slot, holder != null && holder.HighlightSlot == slot);
    }

    /// 빈 참조는 null. 취소는 호출자에게 넘기고, 그 밖의 실패는 알리고 null을 준다.
    static async UniTask<GameObject> LoadAsync(int slot, AssetReference reference)
    {
        if (reference == null || !reference.RuntimeKeyIsValid()) return null;
        try
        {
            return await ResourceManager.Instance.LoadAsync<GameObject>(reference);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            CDebug.LogError($"ItemSlotPresenter: 칸 {slot} 아이템을 불러오지 못했다. {e.Message}");
            return null;
        }
    }

    /// 상태별 머티리얼로 갈아 끼운다 (기획서 5.3.1). 무엇을 무엇으로 바꿀지는 `ItemVisualConfig`의 표가 정한다.
    void Restate(GameObject root, bool isDirty, bool isBurnt, bool isToasted)
    {
        if (!isDirty && !isBurnt && !isToasted) return;
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            var materials = r.sharedMaterials;
            for (var i = 0; i < materials.Length; i++)
                materials[i] = config.StateOf(materials[i], isDirty, isBurnt, isToasted);
            r.sharedMaterials = materials;
        }
    }

    static GameObject Place(GameObject prefab, Transform anchor, int layer)
    {
        var made = UnityEngine.Object.Instantiate(prefab, anchor);
        made.transform.localPosition = Vector3.zero;
        made.transform.localRotation = Quaternion.identity;
        SetLayer(made, layer);
        return made;
    }

    /// 내용물을 그릇에 붙인다. 접시는 윗면에 얹고, 잔은 폭의 `fill`만큼으로 줄여 윗면이
    /// 테두리에 오도록 담는다. 둘 다 내용물 가운데를 그릇 가운데에 맞춘다.
    static void Seat(Transform item, Transform vessel, bool inside, float fill)
    {
        // 붙이기 전에 잰다. 붙인 뒤면 그릇 경계에 내용물까지 잡힌다.
        var dish = LocalBounds(vessel);
        var food = LocalBounds(item);

        var width = Mathf.Max(food.size.x, food.size.z);
        var scale = inside && width > 0f ? Mathf.Min(1f, dish.size.x * fill / width) : 1f;
        var y = inside ? dish.max.y - food.max.y * scale : dish.max.y - food.min.y * scale;

        item.SetParent(vessel, false);
        item.localScale = Vector3.one * scale;
        item.localPosition = new Vector3(dish.center.x - food.center.x * scale, y, dish.center.z - food.center.z * scale);
    }

    /// 루트 로컬 공간의 메시 경계. 렌더러 경계는 월드 AABB라 자리가 돌아 있으면 부푼다.
    static Bounds LocalBounds(Transform root)
    {
        var bounds = new Bounds();
        var first = true;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null) continue;
            var mesh = filter.sharedMesh.bounds;
            for (var corner = 0; corner < 8; corner++)
            {
                var local = mesh.center + Vector3.Scale(mesh.extents, new Vector3(
                    (corner & 1) == 0 ? -1f : 1f, (corner & 2) == 0 ? -1f : 1f, (corner & 4) == 0 ? -1f : 1f));
                var point = root.InverseTransformPoint(filter.transform.TransformPoint(local));
                if (first) { bounds = new Bounds(point, Vector3.zero); first = false; }
                else bounds.Encapsulate(point);
            }
        }
        return bounds;
    }

    void ApplyHighlight(int slot, bool lit)
    {
        if (standing[slot] == null) return;
        standing[slot].transform.localPosition = lit ? highlightOffset : Vector3.zero;
        standing[slot].transform.localScale = Vector3.one * (lit ? highlightScale : 1f);

        // 내용이 바뀔 때만 불린다 (`Refresh`). 매 프레임 도는 조회가 아니다.
        standing[slot].GetComponentsInChildren(true, Renderers);
        foreach (var r in Renderers)
        {
            // 파티클·라인에 테두리를 두르면 쿼드 모양이 뜬다 (`TargetOutline.Collect`와 같은 거름).
            if (r is not MeshRenderer and not SkinnedMeshRenderer) continue;
            r.renderingLayerMask = lit ? r.renderingLayerMask | NextPickBit : r.renderingLayerMask & ~NextPickBit;
        }
    }

    static void SetLayer(GameObject root, int layer)
    {
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    void ClearSlot(int slot)
    {
        if (standing[slot] != null)
        {
            UnityEngine.Object.Destroy(standing[slot]);
            standing[slot] = null;
        }
        if (sources[slot] != null && sources[slot].RuntimeKeyIsValid())
            ResourceManager.Instance.Release<GameObject>(sources[slot]);
        if (dishSources[slot] != null && dishSources[slot].RuntimeKeyIsValid())
            ResourceManager.Instance.Release<GameObject>(dishSources[slot]);
        sources[slot] = null;
        dishSources[slot] = null;
        burnt[slot] = false;
        dirty[slot] = false;
        toasted[slot] = false;
    }

    /// 전 칸을 비운다. 진행 중인 로드도 무효화한다.
    public void Clear()
    {
        holder = null;
        for (var slot = 0; slot < standing.Length; slot++)
        {
            generation[slot]++;
            ClearSlot(slot);
        }
    }
}
