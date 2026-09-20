using System.Collections.Generic;
using UnityEngine;

/// 지금 F가 닿는 설비에 테두리를 두른다 (기획서 5.7.4 「손 상태로 불가능한 프롬프트는 숨긴다」).
///
/// **설비마다 붙이지 않고 로컬 플레이어에 하나만 붙인다.** 설비 쪽에서 트리거로 켜면
/// 사거리 안의 설비가 전부 켜지는데, 카페의 재료 칸은 3.1m 간격으로 늘어서 있어서
/// (`CafeLayoutSetup`) 한가운데 서면 둘이 같이 켜지고 프롬프트는 하나만 뜬다. 여기서
/// `PlayerInteractor.Target`을 읽으면 **프롬프트가 가리키는 그 하나**만 켜진다.
/// `ItemBoxView`에 같은 취지의 ponytail 주석이 남아 있다 — 밤 상자는 안개 게이트가
/// 얽혀 있어 자기 테두리를 그대로 둔다.
///
/// 테두리는 메시마다 **두 벌**을 세워 만든다 (`Art/Shaders/StationOutline.shader`).
/// 마스크 벌이 원래 크기로 스텐실을 찍고, 테두리 벌이 살짝 키운 같은 메시를 그 자리만
/// 빼고 그린다. 렌더 파이프라인은 건드리지 않는다 — URP 렌더러 에셋에 패스를 추가하지
/// 않고 머티리얼의 렌더 상태만으로 성립한다.
[RequireComponent(typeof(PlayerInteractor))]
public class TargetOutline : MonoBehaviour
{
    /// 테두리 벌. `StationOutline.mat`.
    [SerializeField] Material outlineMaterial;

    /// 마스크 벌. `StationOutlineMask.mat`. 색을 쓰지 않고 스텐실 비트만 찍는다.
    [SerializeField] Material maskMaterial;

    /// 테두리 벌을 본체보다 얼마나 키울지. 1에 가까울수록 테두리가 얇다.
    [SerializeField, Range(1.01f, 1.3f)] float outlineScale = 1.06f;

    /// 테두리 색. 1을 넘는 값은 블룸에 실으라고 둔 것이다.
    [SerializeField, ColorUsage(true, true)]
    Color outlineColor = new(1.5f, 1.5f, 1.75f, 1f);

    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    /// 메시 하나에 붙는 두 벌.
    readonly struct Shell
    {
        public readonly Renderer Mask;
        public readonly Renderer Edge;
        public Shell(Renderer mask, Renderer edge) { Mask = mask; Edge = edge; }
    }

    PlayerInteractor interactor;

    /// 대상별로 한 번 만들어 둔 벌. 설비는 판이 끝날 때까지 살아 있어서 다시 만들 일이 없다.
    /// ponytail: 파괴된 설비의 칸은 남는다. 설비 수가 두 자릿수라 비우는 코드가 더 비싸다.
    readonly Dictionary<MonoBehaviour, Shell[]> shells = new();

    /// 지금 테두리가 붙어 있는 대상.
    MonoBehaviour lit;

    void Awake() => interactor = GetComponent<PlayerInteractor>();

    void OnDisable()
    {
        Toggle(lit, false);
        lit = null;
    }

    /// **대상이 바뀐 프레임에만 일한다.** 나머지 프레임은 비교 한 번으로 끝난다.
    void Update()
    {
        if (interactor == null || !interactor.IsOwner) return;

        // `Nearest()`는 후보 목록만 훑는다. 컴포넌트 조회가 아니다 (AGENTS.md 참조와 결합도).
        var target = interactor.Target as MonoBehaviour;
        if (target == lit) return;

        Toggle(lit, false);
        lit = target;
        Toggle(lit, true);
    }

    void Toggle(MonoBehaviour target, bool on)
    {
        if (target == null || outlineMaterial == null || maskMaterial == null) return;

        var parts = on ? ShellsOf(target) : Cached(target);
        if (parts == null) return;

        foreach (var part in parts)
        {
            if (part.Mask != null) part.Mask.enabled = on;
            if (part.Edge != null) part.Edge.enabled = on;
        }
    }

    Shell[] Cached(MonoBehaviour target) =>
        shells.TryGetValue(target, out var parts) ? parts : null;

    /// 대상의 메시마다 두 벌을 세운다. **대상이 처음 잡힐 때 한 번만 돈다** — 조회가
    /// 프레임 수에 비례하지 않으므로 허용되는 자리다 (AGENTS.md).
    ///
    /// 벌을 부품의 자식으로 두는 이유는 그래야 부품의 월드 자리를 그대로 따라가기 때문이다.
    /// 설비 루트 밑에 모아 두면 부품마다 상대 좌표를 손으로 맞춰야 하고, 한 번 어긋나면
    /// 테두리가 설비 옆에 떠 있는다.
    Shell[] ShellsOf(MonoBehaviour target)
    {
        var cached = Cached(target);
        if (cached != null) return cached;

        var built = new List<Shell>();

        // 밤 상자는 자기 테두리를 갖고 있고 안개 게이트까지 얽혀 있다 (`ItemBoxView`).
        // 여기서 또 씌우면 그 발광 셸 위에 테두리가 하나 더 붙는다.
        if (target.GetComponent<ItemBoxView>() != null) return Remember(target, built);

        foreach (var filter in target.GetComponentsInChildren<MeshFilter>(true))
        {
            // 메시만 있고 렌더러가 없는 것은 화면에 없는 것이다 — 충돌용 메시에 테두리를
            // 두르면 설비보다 큰 상자가 떠오른다.
            if (filter.sharedMesh == null || filter.GetComponent<MeshRenderer>() == null) continue;

            var mask = Build(filter, maskMaterial, 1f);

            // **메시 중심을 기준으로 키운다.** 피벗 기준으로 키우면 피벗이 한쪽에 쏠린
            // 부품(상판·문짝처럼 납작한 것)의 벌이 통째로 밀려서, 테두리가 한쪽에만 난다.
            var edge = Build(filter, outlineMaterial, outlineScale);
            edge.transform.localPosition = filter.sharedMesh.bounds.center * (1f - outlineScale);

            var block = new MaterialPropertyBlock();
            block.SetColor(BaseColorId, outlineColor);
            edge.SetPropertyBlock(block);

            built.Add(new Shell(mask, edge));
        }

        return Remember(target, built);
    }

    Renderer Build(MeshFilter source, Material material, float scale)
    {
        var part = new GameObject(material.name);

        // 카페는 팀 레이어에 있고 카메라가 그것으로 컬링한다 (`TeamVision`). 부품의
        // 레이어를 따라가지 않으면 테두리만 상대 팀 화면에 떠 있는다 (`ItemDisplay.Build`).
        part.layer = source.gameObject.layer;
        part.transform.SetParent(source.transform, false);
        part.transform.localScale = Vector3.one * scale;

        part.AddComponent<MeshFilter>().sharedMesh = source.sharedMesh;

        var renderer = part.AddComponent<MeshRenderer>();

        // **서브메시 수만큼 꽂는다.** 하나만 꽂으면 서브메시 0만 그려진다 — 키트 모델은
        // 색 영역마다 서브메시가 갈려 있어서(캐비닛 본체와 상판이 2개) 테두리가 모델의
        // 일부에만 나고 나머지는 통째로 사라진다.
        var slots = new Material[Mathf.Max(1, source.sharedMesh.subMeshCount)];
        for (var i = 0; i < slots.Length; i++) slots[i] = material;
        renderer.sharedMaterials = slots;

        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.enabled = false;

        return renderer;
    }

    /// 만든 벌을 대상에 묶어 둔다. 빈 목록도 기억한다 — 스킨드 메시뿐인 팀원처럼 벌이
    /// 나올 수 없는 대상을 매번 다시 훑지 않기 위한 것이다.
    Shell[] Remember(MonoBehaviour target, List<Shell> built)
    {
        var parts = built.ToArray();
        shells[target] = parts;
        return parts;
    }
}
