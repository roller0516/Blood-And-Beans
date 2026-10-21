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

    /// 1~3등급 뚜껑 모델. 순서가 곧 등급이고 `body`의 자식이라 정규화한 크기를 그대로 따른다.
    /// 닫힌 자세는 `tierMeshes`와 같다. 비워 두면 뚜껑 연출 없이 닫힌 본체만 간다 — 쏟아진 더미처럼
    /// 열릴 필요 없는 상자.
    [SerializeField] ChestModel[] tierModels = new ChestModel[3];

    /// 3등급 발광 아웃라인. 없으면 발광 없이 동작한다 — 임시 더미처럼 필요 없는 상자가 있다.
    [SerializeField] Renderer glow;

    /// 아웃라인 헐을 본체보다 얼마나 키울지. 1에 가까울수록 테두리가 얇다.
    /// 너무 키우면 테두리가 아니라 상자를 감싼 덩어리가 된다.
    [SerializeField, Range(1.01f, 1.3f)] float outlineScale = 1.07f;

    /// 발광을 켜기 시작하는 등급.
    [SerializeField] int glowFromTier = 3;

    /// 본체를 이 높이(월드 유닛)로 맞춘다. Kenney 모델은 팩마다 크기 기준이 달라서
    /// (survival-kit 상자는 한 변 0.25) 그대로 끼우면 등급마다 크기가 튄다. 메시 경계로
    /// 정규화하면 어떤 모델을 물려도 상자가 같은 크기로 보인다.
    [SerializeField] float bodyHeight = 0.9f;

    ItemBox box;
    MeshFilter bodyMesh;
    MeshFilter glowMesh;
    int appliedTier;

    /// 상호작용 테두리는 `TargetOutline`이 이 본체에만 두른다. 안개에 가려 꺼져 있으면 테두리도 없다.
    public Renderer Body => body;

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
    /// 메시를 그대로 붙이면 반 높이만큼 공중에 뜬다. 중력이 없어서(PlayerController) 한번 뜨면
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

        // 등급은 매 밤 리롤된다 (기획서 6.3). 바뀐 밤에만 머티리얼을 갈아 끼운다 —
        // 매 프레임 대입하면 SRP 배칭이 깨지고 인스턴스 머티리얼이 새로 생긴다.
        var tier = box.Tier;
        if (tier != appliedTier)
        {
            appliedTier = tier;
            var index = tier - 1;
            if (index >= 0 && index < tierMaterials.Length && tierMaterials[index] != null)
            {
                body.sharedMaterial = tierMaterials[index];
                if (index < tierModels.Length && tierModels[index] != null)
                    tierModels[index].SetMaterial(tierMaterials[index]);
            }
            for (var i = 0; i < tierModels.Length; i++)
                if (i != index && tierModels[i] != null) tierModels[i].gameObject.SetActive(false);
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

        var lidShown = SyncLid(tier, cleared);
        body.enabled = cleared && !lidShown;

        // 발광은 안개와 무관하다 (기획서 6.5.2). 위치만 알려 주고 내용은 알려 주지 않는다.
        if (glow != null) glow.enabled = tier >= glowFromTier;
    }

    /// 뚜껑이 움직이는 동안은 닫힌 본체 대신 애니메이션 모델을 보인다. 두 모델의 닫힌 자세가
    /// 같아서 바꿔 끼워도 티가 안 난다. 열림은 서버가 복제한 값(`ItemBox.LidOpen`)만 읽는다 —
    /// 누가 열었든 모두의 화면에서 같은 상태다. 모델을 보였으면 true.
    bool SyncLid(int tier, bool cleared)
    {
        var index = tier - 1;
        var model = index >= 0 && index < tierModels.Length ? tierModels[index] : null;
        if (model == null) return false;

        // 닫는 모션이 끝나 가라앉을 때까지 켜 둔다. 열림 신호가 꺼지는 순간 끄면 뚜껑이 뚝 닫힌다.
        var show = box.LidOpen || !model.Settled;
        model.gameObject.SetActive(show);
        if (!show) return false;

        model.SetOpen(box.LidOpen);
        model.SetVisible(cleared);
        return true;
    }
}
