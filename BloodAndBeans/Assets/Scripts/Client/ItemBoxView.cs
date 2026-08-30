using UnityEngine;

/// 상자의 겉모습. 게임 규칙(`ItemBox`)에서 표현을 떼어 놓는다.
///
/// 두 가지를 한다.
/// 1. **안개 게이트** — 상자 본체는 그 위의 안개가 걷힌 뒤에야 보인다 (기획서 6.1-2).
///    안개는 화면 전체 패스라 불투명 상자를 덮지만 알파가 0.96이라 4%가 비친다. 그
///    4%로도 안 걷힌 자리의 상자 위치가 읽히므로 렌더러를 아예 끈다.
/// 2. **등급 표현** — 기획서 6.5.2는 "등급은 원거리에서 형태·재질·색·발광으로 구분된다"고
///    정했다. 등급별 머티리얼을 갈아 끼우고, 3등급이면 발광 셸을 켠다.
///
/// 발광 셸에는 안개 게이트를 걸지 않는다. 6.5.2가 "3등급 박스는 안개 너머에서도 희미하게
/// 빛이 새어 나온다"고 못 박았기 때문이다. 셸 머티리얼(`ItemBoxGlow`)이 Transparent 큐라
/// 안개 패스(BeforeRenderingTransparents) 뒤에 그려져서 실제로 안개를 통과한다.
[RequireComponent(typeof(ItemBox))]
public class ItemBoxView : MonoBehaviour
{
    [SerializeField] Renderer body;

    /// 1~3등급 머티리얼. 순서가 곧 등급이다.
    [SerializeField] Material[] tierMaterials = new Material[3];

    /// 1~3등급 메시. 기획서 6.5.2가 등급을 "형태·재질·색·발광"으로 구분한다고 했으므로
    /// 색만 바꾸면 부족하다 — 1등급 나무 상자, 2등급 철제 궤, 3등급 룬 상자.
    /// 비워 두면 형태는 바꾸지 않고 머티리얼만 간다.
    [SerializeField] Mesh[] tierMeshes = new Mesh[3];

    /// 3등급 발광 아웃라인. 없으면 발광 없이 동작한다 — 임시 더미처럼 필요 없는 상자가 있다.
    [SerializeField] Renderer glow;

    /// 아웃라인 헐을 본체보다 얼마나 키울지. 1에 가까울수록 테두리가 얇다.
    /// 너무 키우면 테두리가 아니라 상자를 감싼 덩어리가 된다.
    [SerializeField, Range(1.01f, 1.3f)] float outlineScale = 1.07f;

    /// 발광을 켜기 시작하는 등급.
    [SerializeField] int glowFromTier = 3;

    /// 상호작용 테두리 색. 등급 발광과 같은 셸을 쓰지만 색은 갈라야 한다 — 기획서 6.5.2가
    /// 3등급 발광을 "금보라"로 정해 뒀으므로 머티리얼 색 자체를 흰색으로 바꿀 수는 없다.
    /// 1을 넘는 값은 블룸에 실리라고 둔 것이다(등급 색도 같은 범위를 쓴다).
    [SerializeField, ColorUsage(true, true)]
    Color highlightColor = new Color(1.5f, 1.5f, 1.5f, 1f);

    /// 본체를 이 높이(월드 유닛)로 맞춘다. Kenney 모델은 팩마다 크기 기준이 달라서
    /// (survival-kit 상자는 한 변 0.25) 그대로 끼우면 등급마다 크기가 튄다. 메시 경계로
    /// 정규화하면 어떤 모델을 물려도 상자가 같은 크기로 보인다.
    [SerializeField] float bodyHeight = 0.9f;

    ItemBox box;
    MeshFilter bodyMesh;
    MeshFilter glowMesh;
    int appliedTier;

    /// 셰이더의 테두리 색 프로퍼티. 이름 해석은 한 번만 한다.
    static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");

    MaterialPropertyBlock glowBlock;

    /// 지금 셸에 칠해 둔 색이 상호작용 색인가. 매 프레임 블록을 다시 쓰면 등급 발광
    /// 상태에서도 계속 덮어써서 SRP 배칭이 깨진다 — 바뀔 때만 건드린다.
    bool highlightTinted;

    /// 상호작용 범위 안에 있는 로컬 플레이어 콜라이더 수. bool이 아니라 세는 이유는
    /// 플레이어 하나가 트리거를 여럿 물릴 수 있어서다 — 하나가 빠질 때 나머지를 무시하면
    /// 테두리가 붙은 채로 남는다.
    int nearbyLocal;

    void Awake()
    {
        box = GetComponent<ItemBox>();
        if (body == null) body = GetComponent<Renderer>();
        if (body != null) bodyMesh = body.GetComponent<MeshFilter>();
        if (glow != null) glowMesh = glow.GetComponent<MeshFilter>();
        if (body == null)
            Debug.LogError($"{name}: 상자 본체 Renderer가 없다. Inspector의 body에 연결해야 "
                         + "안개 게이트와 등급 머티리얼이 걸린다.", this);
    }

    /// 메시를 `bodyHeight` 높이로 맞추고 밑면을 지면에 붙인다. 본체는 별도 자식이라
    /// 여기서 스케일과 위치를 바꿔도 콜라이더와 발광 셸은 그대로다.
    ///
    /// 위치 보정이 필요한 이유는 원점 규약이 달라서다. Unity 기본 Cube는 원점이 중심이지만
    /// Kenney 모델은 밑동이다(`box`는 y가 0~0.25). 상자 루트의 원점은 상자 *중심*이므로,
    /// 메시를 그대로 붙이면 반 높이만큼 공중에 뜬다. 중력이 없어서(PlayerMove) 한번 뜨면
    /// 스스로 내려오지 않는다.
    void Normalise(Mesh mesh)
    {
        var height = mesh.bounds.size.y;
        if (height <= Mathf.Epsilon) return;

        var scale = bodyHeight / height;
        bodyMesh.transform.localScale = Vector3.one * scale;
        bodyMesh.transform.localPosition =
            new Vector3(0f, -bodyHeight * 0.5f - mesh.bounds.min.y * scale, 0f);
    }

    void Update()
    {
        if (box == null || body == null) return;

        var cleared = box.Cleared;
        body.enabled = cleared;

        // 등급은 매 밤 리롤된다 (기획서 6.3). 바뀐 밤에만 머티리얼을 갈아 끼운다 —
        // 매 프레임 대입하면 SRP 배칭이 깨지고 인스턴스 머티리얼이 새로 생긴다.
        var tier = box.Tier;
        if (tier != appliedTier)
        {
            appliedTier = tier;
            var index = tier - 1;
            if (index >= 0 && index < tierMaterials.Length && tierMaterials[index] != null)
                body.sharedMaterial = tierMaterials[index];
            if (bodyMesh != null && index >= 0 && index < tierMeshes.Length && tierMeshes[index] != null)
            {
                bodyMesh.sharedMesh = tierMeshes[index];
                Normalise(bodyMesh.sharedMesh);

                // 아웃라인은 본체와 *같은 메시*를 살짝 키워 그린다. 그래야 테두리가 등급마다
                // 달라지는 상자 모양을 그대로 따라간다 — 구 껍데기는 상자를 가리는 공이 된다.
                if (glowMesh != null)
                {
                    glowMesh.sharedMesh = bodyMesh.sharedMesh;
                    glowMesh.transform.localScale = bodyMesh.transform.localScale * outlineScale;
                    glowMesh.transform.localPosition = bodyMesh.transform.localPosition;
                }
            }
        }

        // 발광은 안개와 무관하다 (기획서 6.5.2). 위치만 알려 주고 내용은 알려 주지 않는다.
        // 상호작용 테두리는 같은 셸을 재사용하되 안개 게이트를 건다 — 안 걷힌 자리의
        // 상자 위치를 테두리가 대신 알려 주면 안개를 둔 의미가 없다.
        if (glow == null) return;

        var highlighted = nearbyLocal > 0 && cleared;
        glow.enabled = tier >= glowFromTier || highlighted;
        Tint(highlighted);
    }

    /// 상호작용 중이면 흰 테두리, 아니면 머티리얼의 등급 색으로 되돌린다. 3등급 상자
    /// 앞에 서면 금보라가 잠시 흰색이 된다 — 지금 무엇을 잡을 수 있는지가 등급 표시보다
    /// 급한 정보고, 등급은 앞에 선 시점에 이미 형태와 재질로 읽힌다 (기획서 6.5.2).
    void Tint(bool highlighted)
    {
        if (highlighted == highlightTinted) return;
        highlightTinted = highlighted;

        glowBlock ??= new MaterialPropertyBlock();
        glow.GetPropertyBlock(glowBlock);
        if (highlighted) glowBlock.SetColor(GlowColorId, highlightColor);
        else glowBlock.Clear();          // 비우면 머티리얼 값(금보라)으로 돌아간다
        glow.SetPropertyBlock(glowBlock);
    }

    // ponytail: 사거리 안의 상자가 전부 켜진다. 프롬프트는 가장 가까운 하나만 뜨므로
    // 상자가 겹쳐 놓인 자리에서는 테두리 둘에 프롬프트 하나가 된다. 거슬리면
    // PlayerInteractor가 "가장 가까운 대상"을 알려 주는 경로를 따로 낸다.
    void OnTriggerEnter(Collider other)
    {
        if (IsLocalPlayer(other)) nearbyLocal++;
    }

    void OnTriggerExit(Collider other)
    {
        if (IsLocalPlayer(other) && nearbyLocal > 0) nearbyLocal--;
    }

    /// 상자가 꺼졌다 켜지는 동안의 Exit는 오지 않는다. 세어 둔 값을 비워야 테두리가
    /// 아무도 없는 자리에 남지 않는다.
    void OnDisable() => nearbyLocal = 0;

    /// 로컬 플레이어의 상호작용 사거리인지 본다. 소유자 검사를 빼면 남의 캐릭터가
    /// 지나갈 때도 켜져서, 테두리가 적의 위치를 알려 주는 신호가 된다.
    static bool IsLocalPlayer(Collider other)
    {
        var interactor = other.GetComponentInParent<PlayerInteractor>();
        return interactor != null && interactor.IsOwner;
    }
}
