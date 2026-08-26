using Unity.Netcode;
using UnityEngine;

/// 종족마다 다른 몸과 색을 준다. 기획서 5.5에서 종족별로 인내심과 가격이 다르므로,
/// 매장 반대편에서 구분되는 것은 장식이 아니라 게임플레이 정보다.
/// ponytail: CC0 인간 키트를 색만 바꿔 임시로 쓴다. 아트 파이프라인이 생기면 메시만
/// 진짜 언데드 모델로 교체하면 되고 나머지는 바꿀 것이 없다.
public class CustomerLook : NetworkBehaviour
{
    [SerializeField] GameObject[] bySpecies = new GameObject[6];

    static readonly Color[] Tint =
    {
        new(0.45f, 0.65f, 0.35f),   // 좀비     — 병색 도는 녹색
        new(0.62f, 0.12f, 0.16f),   // 뱀파이어 — 핏빛 붉은색
        new(0.70f, 0.85f, 0.95f),   // 유령     — 창백한 푸른색
        new(0.92f, 0.90f, 0.82f),   // 해골     — 뼈색
        new(0.40f, 0.28f, 0.18f),   // 늑대인간 — 어두운 털색
        new(0.45f, 0.25f, 0.60f),   // 마녀     — 보라색
    };

    [SerializeField] float bodyHeight = 1.6f;   // 월드 단위. 종족마다 키를 맞추기 위한 값

    Species? shown;
    GameObject body;

    public override void OnNetworkSpawn()
    {
        var c = GetComponent<Customer>();
        c.SpeciesChanged += Apply;
        Apply(c.Kind);
    }

    public override void OnNetworkDespawn()
    {
        var c = GetComponent<Customer>();
        if (c != null) c.SpeciesChanged -= Apply;
    }

    void Apply(Species s)
    {
        if (shown == s) return;
        var i = (int)s;
        if (i < 0 || i >= bySpecies.Length || bySpecies[i] == null) return;
        shown = s;

        // Destroy는 프레임 끝으로 미뤄지므로 이름으로 찾으면 이번 프레임에는 여전히 옛
        // 몸이 잡힌다. 참조를 들고 있다가 명시적으로 버린다.
        if (body != null) { body.name = "Body(old)"; Destroy(body); }

        // 임시 캡슐을 숨기고 종족 몸을 보여 준다
        var own = GetComponent<MeshRenderer>();
        if (own != null) own.enabled = false;

        body = Instantiate(bySpecies[i], transform, false);
        body.name = "Body";
        body.transform.localPosition = Vector3.zero;   // 모델 피벗이 발밑에 있다
        body.transform.localScale = Vector3.one;

        // Kenney 캐릭터는 리깅돼 있어 렌더러가 Mesh가 아니라 Skinned다. MeshRenderer만
        // 물들이면 모든 종족이 같은 색으로 남았다.
        var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { color = Tint[i] };
        foreach (var r in body.GetComponentsInChildren<Renderer>()) r.sharedMaterial = mat;

        // 원본 키트마다 스케일 기준이 달라서, 키트별 매직 넘버를 믿지 않고 실제로 잰
        // 높이로 정규화한다.
        var h = MeasuredHeight(body);
        if (h > 0.001f) body.transform.localScale = Vector3.one * (bodyHeight / h);
    }

    static float MeasuredHeight(GameObject go)
    {
        var rs = go.GetComponentsInChildren<Renderer>();
        if (rs.Length == 0) return 0f;
        var b = rs[0].bounds;
        for (int k = 1; k < rs.Length; k++) b.Encapsulate(rs[k].bounds);
        return b.size.y;
    }
}
