using System;
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
    readonly ItemVisualConfig config;
    readonly Transform[] anchors;
    readonly float highlightScale;
    readonly Vector3 highlightOffset;

    readonly GameObject[] standing;
    readonly AssetReference[] sources;
    readonly bool[] burnt;
    readonly int[] generation;

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
        burnt = new bool[count];
        generation = new int[count];
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
            view.Burnt |= view.Dirty;
            var reference = config.ReferenceFor(view);

            if (!SameKey(reference, sources[slot]) || view.Burnt != burnt[slot])
                RebuildSlotAsync(slot, reference, view.Burnt, anchor, anchor.gameObject.layer).Forget();

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

    async UniTaskVoid RebuildSlotAsync(int slot, AssetReference reference, bool isBurnt, Transform anchor, int layer)
    {
        var myGeneration = ++generation[slot];

        GameObject prefab = null;
        if (reference != null && reference.RuntimeKeyIsValid())
        {
            try
            {
                prefab = await ResourceManager.Instance.LoadAsync<GameObject>(reference);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception e)
            {
                CDebug.LogError($"ItemSlotPresenter: 칸 {slot} 아이템을 불러오지 못했다. {e.Message}");
            }
        }

        // 그사이 이 칸의 내용이 다시 바뀌었다. 이번 결과는 버린다.
        if (myGeneration != generation[slot])
        {
            if (prefab != null) ResourceManager.Instance.Release<GameObject>(reference);
            return;
        }

        ClearSlot(slot);
        sources[slot] = reference;
        burnt[slot] = isBurnt;

        if (prefab == null || anchor == null) return;

        var made = UnityEngine.Object.Instantiate(prefab, anchor);
        made.transform.localPosition = Vector3.zero;
        made.transform.localRotation = Quaternion.identity;
        SetLayer(made, layer);

        if (isBurnt && config.Burnt != null)
            foreach (var r in made.GetComponentsInChildren<Renderer>(true))
                r.sharedMaterial = config.Burnt;

        standing[slot] = made;

        // 로드가 끝나는 사이 강조 칸이 바뀌었을 수 있다. holder는 살아 있는 참조라 지금
        // 값을 다시 물으면 된다.
        ApplyHighlight(slot, holder != null && holder.HighlightSlot == slot);
    }

    void ApplyHighlight(int slot, bool lit)
    {
        if (standing[slot] == null) return;
        standing[slot].transform.localPosition = lit ? highlightOffset : Vector3.zero;
        standing[slot].transform.localScale = Vector3.one * (lit ? highlightScale : 1f);
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
        sources[slot] = null;
        burnt[slot] = false;
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
