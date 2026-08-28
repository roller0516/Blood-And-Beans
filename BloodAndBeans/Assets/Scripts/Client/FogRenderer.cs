using System.Collections.Generic;
using UnityEngine;

/// 판 전체가 공유하는 안개를 지면 높이의 사각형 하나에 그린다.
/// FogOfWar와 분리한 것은 의도다. 상태는 게임플레이라 동기화되지만 보이는 방식은 아니므로,
/// 더 나은 셰이더로 교체해도 로직은 건드리지 않는다.
///
/// CPU는 셀당 텍셀 하나짜리 마스크에 걷힘 여부만 쓴다. 가장자리를 둥글게 만드는 일은
/// BloodAndBeans/FogOfWar 셰이더가 한다. 예전처럼 CPU에서 텍셀마다 원을 찍고 박스 블러를
/// 돌리면 한 번 칠할 때마다 720x720을 훑느라 100ms 넘게 멈춰서, 개척하는 사람이 있는 내내
/// 이동이 끊겼다.
[RequireComponent(typeof(MeshRenderer))]
public class FogRenderer : MonoBehaviour
{
    [SerializeField] Color fogColor = new(0.05f, 0.06f, 0.10f, 0.96f);
    [SerializeField] Shader fogShader;

    /// 셀 격자가 사각형으로 보이지 않게 셰이더가 뭉개는 정도. 마스크의 밉 레벨 단위라
    /// 1 올릴 때마다 평균 범위가 두 배가 된다. 0은 뭉개지 않음(셀 격자가 그대로 보인다).
    [SerializeField, Range(0f, 4f)] float blurLevel = 1.5f;
    [SerializeField, Range(0.01f, 0.5f)] float edgeSoftness = 0.35f;

    static readonly int MaskProperty = Shader.PropertyToID("_MainTex");
    static readonly int FogColorProperty = Shader.PropertyToID("_FogColor");
    static readonly int SoftnessProperty = Shader.PropertyToID("_Softness");
    static readonly int BlurProperty = Shader.PropertyToID("_BlurLevel");

    const byte Revealed = 255;
    const byte Hidden = 0;

    FogOfWar fog;
    Texture2D mask;
    byte[] cells;                                // 마스크의 CPU 사본. 바뀐 칸만 여기에 쓴다
    bool maskDirty;

    MeshRenderer meshRenderer;
    MatchDirector director;
    System.Action<IReadOnlyList<int>> onChanged;

    void Awake() => meshRenderer = GetComponent<MeshRenderer>();

    /// Awake가 아니라 Start에서 찾는다. MatchDirector는 자기 Awake에서 GamePhase를 잡으므로
    /// 두 Awake 사이의 순서에 기대면 `Phase`가 아직 null일 수 있다.
    void Start()
    {
        director = MatchDirector.Instance;
        if (director == null)
            Debug.LogError($"{name}: 씬에 MatchDirector가 없다. 페이즈를 알 수 없어 "
                         + "안개를 밤에만 그릴 수 없다.", this);

        if (fogShader == null)
            Debug.LogError($"{name}: 안개 셰이더가 연결되지 않았다. "
                         + "Assets/Art/Shaders/FogOfWar.shader를 Inspector에 넣어야 한다.", this);
    }

    /// static 이벤트라 씬을 다시 열어도 구독이 남는다. 반드시 짝을 맞춘다.
    void OnDestroy()
    {
        if (onChanged != null) FogOfWar.Changed -= onChanged;
    }

    void Update()
    {
        // 안개는 밤 전용이다 (기획서 6.1). FogOfWar.Update는 낮에 안개를 걷지 않으므로,
        // 판을 그대로 두면 어젯밤 지나간 자리만 뚫린 채 자기 카페 바닥까지 덮여서
        // 낮 2분 동안 걸어도 걷히지 않는다.
        meshRenderer.enabled = director != null && director.Phase.Current == Phase.Night;

        // 안개는 이제 캐릭터에 붙어 있다. 로컬 플레이어가 스폰되기 전에는 그릴 것이 없고,
        // 다시 스폰되면 인스턴스가 바뀌므로 매 프레임 확인해서 다시 바인딩한다.
        var next = FogOfWar.Local();
        if (next == null || fogShader == null) return;
        if (!ReferenceEquals(next, fog))
        {
            fog = next;
            if (mask == null) Build();
            Subscribe();
            RebuildFromState();
        }

        // 한 프레임에 여러 번 걷혀도 업로드는 한 번이다.
        if (!maskDirty) return;
        maskDirty = false;
        mask.SetPixelData(cells, 0);
        // 밉 갱신을 켠다. 셰이더가 넓은 평균을 밉에서 읽으므로 밉이 낡으면 걷힌 자리가
        // 흐릿하게 남는다. 240x240 R8이라 피라미드 재생성 비용은 무시할 수준이다.
        mask.Apply(true);
    }

    void Subscribe()
    {
        if (onChanged != null) FogOfWar.Changed -= onChanged;

        onChanged = revealed =>
        {
            // null은 전체가 뒤집혔다는 뜻이다(밤 초기화). 그때만 판을 다시 만든다.
            if (revealed == null) { RebuildFromState(); return; }

            for (int i = 0; i < revealed.Count; i++) cells[revealed[i]] = Revealed;
            maskDirty = true;
        };
        FogOfWar.Changed += onChanged;
    }

    /// 현재 걷힘 상태를 마스크에 그대로 옮긴다. 밤 초기화와 늦은 합류 스냅샷이 쓴다.
    void RebuildFromState()
    {
        var count = fog.Side * fog.Side;
        for (int c = 0; c < count; c++) cells[c] = fog.IsRevealedCell(c) ? Revealed : Hidden;
        maskDirty = true;
    }

    /// Inspector에서 값을 돌리면 곧바로 화면에 반영한다. 머티리얼은 Build에서 한 번만
    /// 채우므로 이것이 없으면 플레이를 다시 시작해야 가장자리 감각을 비교할 수 있다.
    void OnValidate()
    {
        if (!Application.isPlaying || meshRenderer == null) return;

        var mat = meshRenderer.sharedMaterial;
        if (mat == null) return;

        mat.SetColor(FogColorProperty, fogColor);
        mat.SetFloat(SoftnessProperty, edgeSoftness);
        mat.SetFloat(BlurProperty, blurLevel);
    }

    void Build()
    {
        var side = fog.Side;

        // 셀당 텍셀 하나면 충분하다. 부드러운 가장자리는 바이리니어 필터와 셰이더가 만든다.
        // 밉맵을 만든다. 셀당 텍셀 하나짜리 마스크에서 넓고 부드러운 가장자리를 얻는
        // 유일한 싼 방법이다. Trilinear라야 밉 사이가 계단 없이 이어진다.
        mask = new Texture2D(side, side, TextureFormat.R8, mipChain: true)
        {
            filterMode = FilterMode.Trilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        cells = new byte[side * side];

        var mat = new Material(fogShader);
        mat.SetTexture(MaskProperty, mask);
        // Unity 기본 Plane은 UV가 월드 XZ 기준으로 180도 돌아가 있다. 이 뒤집기를 넣기
        // 전까지는 플레이어와 반대편 맵에서 안개가 걷혔다.
        mat.mainTextureScale = new Vector2(-1f, -1f);
        mat.mainTextureOffset = new Vector2(1f, 1f);
        mat.SetColor(FogColorProperty, fogColor);
        mat.SetFloat(SoftnessProperty, edgeSoftness);
        mat.SetFloat(BlurProperty, blurLevel);
        meshRenderer.sharedMaterial = mat;

        // 월드 크기는 텍스처 해상도가 아니라 게임플레이 격자에서 온다.
        var span = fog.Side * fog.CellSize;
        transform.localScale = new Vector3(span / 10f, 1f, span / 10f); // Plane 기본 크기는 10 단위
        transform.position = new Vector3(0f, 0.2f, 0f);
    }
}
