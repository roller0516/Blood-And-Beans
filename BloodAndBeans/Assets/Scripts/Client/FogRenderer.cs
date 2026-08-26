using UnityEngine;

/// 판 전체가 공유하는 안개를 지면 높이의 사각형 하나에 텍스처로 그린다.
/// FogOfWar와 분리한 것은 의도다. 상태는 게임플레이라 동기화되지만 보이는 방식은 아니므로,
/// 더 나은 셰이더로 교체해도 로직은 건드리지 않는다.
[RequireComponent(typeof(MeshRenderer))]
public class FogRenderer : MonoBehaviour
{
    [SerializeField] Color fogColor = new(0.05f, 0.06f, 0.10f, 0.96f);
    [SerializeField] int texelsPerCell = 3;     // 셀 하위 해상도. 가장자리를 둥글게 만든다
    [SerializeField] float edgeSoftness = 0.8f; // 셀 단위
    [SerializeField] float blurCells = 2.5f;    // 스무딩 반경. 셀 단위

    FogOfWar fog;
    Texture2D tex;
    MeshRenderer meshRenderer;
    MatchDirector director;
    bool dirty = true;
    System.Action onChanged;

    void Awake()
    {
        meshRenderer = GetComponent<MeshRenderer>();
    }

    /// Awake가 아니라 Start에서 찾는다. MatchDirector는 자기 Awake에서 GamePhase를 잡으므로
    /// 두 Awake 사이의 순서에 기대면 `Phase`가 아직 null일 수 있다.
    void Start()
    {
        director = MatchDirector.Instance;
        if (director == null)
            Debug.LogError($"{name}: 씬에 MatchDirector가 없다. 페이즈를 알 수 없어 "
                         + "안개를 밤에만 그릴 수 없다.", this);
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
        if (next == null) return;
        if (!ReferenceEquals(next, fog))
        {
            if (onChanged != null) FogOfWar.Changed -= onChanged;
            fog = next;
            if (tex == null) Build(); else Rebind();
            dirty = true;
        }

        if (!dirty) return;
        dirty = false;
        Paint();
    }

    /// static 이벤트라 씬을 다시 열어도 구독이 남는다. 반드시 짝을 맞춘다.
    void OnDestroy()
    {
        if (onChanged != null) FogOfWar.Changed -= onChanged;
    }

    void Rebind()
    {
        onChanged = () => dirty = true;
        FogOfWar.Changed += onChanged;
        dirty = true;
    }

    int TexSide => fog.Side * texelsPerCell;

    void Build()
    {
        var side = TexSide;
        tex = new Texture2D(side, side, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };

        // URP는 _Surface만으로는 부족하고 투명 설정 전체를 요구한다. 블렌드 모드와 키워드가
        // 없으면 알파 채널이 무시되어 걷힌 셀이 완전한 검정으로 그려지고 안개가 반전돼 보인다.
        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetFloat("_Surface", 1f);                       // 투명
        mat.SetFloat("_Blend", 0f);                         // 알파 블렌드
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        mat.mainTexture = tex;
        // Unity 기본 Plane은 UV가 월드 XZ 기준으로 180도 돌아가 있다. 이 뒤집기를 넣기
        // 전까지는 플레이어와 반대편 맵에서 안개가 걷혔다.
        mat.mainTextureScale = new Vector2(-1f, -1f);
        mat.mainTextureOffset = new Vector2(1f, 1f);
        meshRenderer.sharedMaterial = mat;

        // 월드 크기는 텍스처 해상도가 아니라 게임플레이 격자에서 온다.
        // 여기서 `side`는 텍셀 수라, 그대로 쓰면 평면이 6배로 늘어났다
        var span = fog.Side * fog.CellSize;
        transform.localScale = new Vector3(span / 10f, 1f, span / 10f); // Plane 기본 크기는 10 단위
        transform.position = new Vector3(0f, 0.2f, 0f);

        onChanged = () => dirty = true;
        FogOfWar.Changed += onChanged;
        dirty = true;
    }

    /// 게임플레이 격자가 정사각형 셀이라 그대로 칠하면 가장자리가 계단이 된다. 대신 걷힌
    /// 셀마다 부드러운 원을 찍고 그 값들을 최댓값으로 합친다. 그러면 걷힌 영역의 바깥
    /// 경계가 둥글게 남는다.
    void Paint()
    {
        var side = TexSide;
        var opaque = (Color32)fogColor;
        // 안개 자체의 RGB를 유지한다. 그래야 부분적으로 걷힌 가장자리가 검게 뜨지 않고 흐려진다
        var rgb = new Color(fogColor.r, fogColor.g, fogColor.b);

        var cover = new float[side * side];   // 0은 안개, 1은 완전히 걷힘

        var radius = 0.5f + edgeSoftness;                  // 셀 단위
        var reach = Mathf.CeilToInt(radius * texelsPerCell);
        var inner = Mathf.Max(0.01f, radius - edgeSoftness);

        var cells = fog.Side;
        for (int c = 0; c < cells * cells; c++)
        {
            if (!fog.IsRevealedCell(c)) continue;

            // 텍셀 좌표계에서의 셀 중심
            var cx = (c % cells + 0.5f) * texelsPerCell;
            var cy = (c / cells + 0.5f) * texelsPerCell;

            for (int dy = -reach; dy <= reach; dy++)
            for (int dx = -reach; dx <= reach; dx++)
            {
                var px = Mathf.FloorToInt(cx) + dx;
                var py = Mathf.FloorToInt(cy) + dy;
                if (px < 0 || py < 0 || px >= side || py >= side) continue;

                var d = Mathf.Sqrt((px + 0.5f - cx) * (px + 0.5f - cx)
                                 + (py + 0.5f - cy) * (py + 0.5f - cy)) / texelsPerCell;
                var a = Mathf.InverseLerp(radius, inner, d);   // 안쪽은 1, 테두리 밖은 0
                var idx = py * side + px;
                if (a > cover[idx]) cover[idx] = a;
            }
        }

        // 원을 찍으면 이웃한 원이 만나는 곳에 물결 모양이 남는다. 분리 가능한 박스 블러
        // 한 번이면 합집합이 하나의 둥근 가장자리가 되고, 그래야 눈에 "원 일곱 개를 그렸다"가
        // 아니라 "내 주변의 안개가 걷혔다"로 읽힌다.
        // 블러 반경은 텍셀이 아니라 셀 단위다. 텍셀 수에 묶어 두면 격자를 촘촘히 하는 순간
        // 가장자리가 다시 날카로워졌다
        Blur(cover, side, Mathf.Max(2, Mathf.RoundToInt(blurCells * texelsPerCell)));

        var pixels = new Color32[side * side];
        for (int i = 0; i < pixels.Length; i++)
        {
            // 하드 클램프가 아니라 smoothstep을 쓴다. 딱 잘라내면 블러로 방금 없앤
            // 다각형 윤곽이 다시 드러난다
            var a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(cover[i]));
            pixels[i] = (Color32)new Color(rgb.r, rgb.g, rgb.b, fogColor.a * (1f - a));
        }

        tex.SetPixels32(pixels);
        tex.Apply(false);
    }

    static void Blur(float[] v, int side, int r)
    {
        var tmp = new float[v.Length];

        for (int y = 0; y < side; y++)
        for (int x = 0; x < side; x++)
        {
            float sum = 0f; int n = 0;
            for (int k = -r; k <= r; k++)
            {
                var sx = x + k;
                if (sx < 0 || sx >= side) continue;
                sum += v[y * side + sx]; n++;
            }
            tmp[y * side + x] = sum / n;
        }

        for (int y = 0; y < side; y++)
        for (int x = 0; x < side; x++)
        {
            float sum = 0f; int n = 0;
            for (int k = -r; k <= r; k++)
            {
                var sy = y + k;
                if (sy < 0 || sy >= side) continue;
                sum += tmp[sy * side + x]; n++;
            }
            v[y * side + x] = sum / n;
        }
    }
}
