using System.Collections.Generic;
using UnityEngine;

/// 지금 F가 닿는 설비에 테두리를 두른다 (기획서 5.7.4 「손 상태로 불가능한 프롬프트는 숨긴다」).
///
/// **설비마다 붙이지 않고 로컬 플레이어에 하나만 붙인다.** 설비 쪽에서 트리거로 켜면
/// 사거리 안의 설비가 전부 켜지는데, 카페의 재료 칸은 3.1m 간격으로 늘어서 있어서
/// (`CafeLayoutSetup`) 한가운데 서면 둘이 같이 켜지고 프롬프트는 하나만 뜬다. 여기서
/// `PlayerController.Target`을 읽으면 **프롬프트가 가리키는 그 하나**만 켜진다.
///
/// 여기서는 대상 렌더러의 렌더링 레이어 비트만 켜고 끈다. 그리는 것은 `OutlineFeature`다.
[RequireComponent(typeof(PlayerController))]
public class TargetOutline : MonoBehaviour
{
    /// `OutlineFeature`가 거르는 렌더링 레이어. TagManager의 0~7번(Default·Light Layer)과 겹치지 않는 첫 비트다.
    public const uint RenderingLayerBit = 1u << 8;

    PlayerController interactor;

    /// 지금 테두리가 붙어 있는 대상.
    MonoBehaviour lit;

    /// 켤 때 잡은 렌더러. 끌 때 다시 찾지 않는다 — 그 사이 설비를 떠난 아이템에 비트가 남지 않게.
    readonly List<Renderer> litRenderers = new();

    void Awake() => interactor = GetComponent<PlayerController>();

    void OnDisable()
    {
        Mark(false);
        litRenderers.Clear();
        lit = null;
    }

    /// **대상이 바뀐 프레임에만 일한다.** 나머지 프레임은 비교 한 번으로 끝난다.
    void Update()
    {
        if (interactor == null || !interactor.IsOwner) return;

        // `Nearest()`는 후보 목록만 훑는다. 컴포넌트 조회가 아니다 (AGENTS.md 참조와 결합도).
        var target = interactor.Target as MonoBehaviour;
        if (target == lit) return;

        Mark(false);
        lit = target;
        Collect(lit);
        Mark(true);
    }

    /// 대상이 바뀔 때 한 번만 돈다 — 조회가 프레임 수에 비례하지 않으므로 허용되는 자리다 (AGENTS.md).
    void Collect(MonoBehaviour target)
    {
        litRenderers.Clear();

        if (target == null) return;

        // 밤 상자는 본체만 두른다 — 등급 발광 셸까지 두르면 셸 바깥에 테두리가 한 겹 더 생긴다.
        // 안개에 가린 상자는 `ItemBoxView`가 본체 렌더러를 꺼 두므로 테두리도 그려지지 않는다.
        if (target.TryGetComponent<ItemBoxView>(out var box))
        {
            if (box.Body != null) litRenderers.Add(box.Body);
            return;
        }

        target.GetComponentsInChildren(litRenderers);

        // 파티클·라인 같은 렌더러에 테두리를 두르면 쿼드 모양 테두리가 뜬다.
        litRenderers.RemoveAll(r => r is not MeshRenderer and not SkinnedMeshRenderer);
    }

    void Mark(bool on)
    {
        foreach (var renderer in litRenderers)
        {
            if (renderer == null) continue;
            renderer.renderingLayerMask = on
                ? renderer.renderingLayerMask | RenderingLayerBit
                : renderer.renderingLayerMask & ~RenderingLayerBit;
        }
    }
}
