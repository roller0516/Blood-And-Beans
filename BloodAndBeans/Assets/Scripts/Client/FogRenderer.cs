using System.Collections.Generic;
using UnityEngine;

/// 판 전체가 공유하는 안개를 화면 전체에 그린다.
/// FogOfWar와 분리한 것은 의도다. 상태는 게임플레이라 동기화되지만 보이는 방식은 아니므로,
/// 더 나은 셰이더로 교체해도 로직은 건드리지 않는다.
///
/// **이 컴포넌트는 더 이상 아무것도 렌더하지 않는다.** 걷힘 마스크와 파라미터를 전역
/// 셰이더 값으로 올려 두기만 하고, 실제로 그리는 것은 URP의 Full Screen Pass Renderer
/// Feature("FogOfWar", PC_Renderer)가 문 `Art/Materials/FogOfWar.mat`이다. 그 머티리얼의
/// 셰이더는 `Art/Shaders/FogOfWar.shadergraph`이고, 계산 본체는 Custom Function 노드가
/// 부르는 `Art/Shaders/FogOfWar.hlsl`에 있다.
///
/// 지면 높이 평면 한 장에 그리던 것을 화면 전체 패스로 옮긴 이유는 높이다. 평면은 y=0.2에
/// 있었고 카메라는 원근(수직에서 30°)이라, 그보다 위에 있는 것은 깊이 검사를 이겨 안개를
/// 뚫고 보였다. 나무를 심는 순간 숲 구조가 안개 너머로 그대로 드러난다. 화면 패스는 깊이
/// 버퍼로 픽셀의 월드 위치를 복원해 그 자리의 걷힘을 읽으므로 높이와 투영에 영향받지 않는다.
///
/// CPU는 셀당 텍셀 하나짜리 마스크에 걷힘 여부만 쓴다. 가장자리를 둥글게 만드는 일은
/// 셰이더가 한다. 예전처럼 CPU에서 텍셀마다 원을 찍고 박스 블러를 돌리면 한 번 칠할 때마다
/// 720x720을 훑느라 100ms 넘게 멈춰서, 개척하는 사람이 있는 내내 이동이 끊겼다.
public class FogRenderer : MonoBehaviour
{
    [SerializeField] Color fogColor = new(0.05f, 0.06f, 0.10f, 0.96f);

    /// 셀 격자가 사각형으로 보이지 않게 셰이더가 뭉개는 정도. 마스크의 밉 레벨 단위라
    /// 1 올릴 때마다 평균 범위가 두 배가 된다. 0은 뭉개지 않음(셀 격자가 그대로 보인다).
    [SerializeField, Range(0f, 4f)] float blurLevel = 1.5f;
    [SerializeField, Range(0.01f, 0.5f)] float edgeSoftness = 0.35f;

    static readonly int MaskProperty = Shader.PropertyToID("_BB_FogMask");
    static readonly int FogColorProperty = Shader.PropertyToID("_BB_FogColor");
    static readonly int SoftnessProperty = Shader.PropertyToID("_BB_FogSoftness");
    static readonly int BlurProperty = Shader.PropertyToID("_BB_FogBlur");
    static readonly int TexelProperty = Shader.PropertyToID("_BB_FogTexel");
    static readonly int WorldToUvProperty = Shader.PropertyToID("_BB_FogWorldToUV");

    const byte Revealed = 255;
    const byte Hidden = 0;

    FogOfWar fog;
    Texture2D mask;
    byte[] cells;                                // 마스크의 CPU 사본. 바뀐 칸만 여기에 쓴다
    bool maskDirty;

    MatchDirector director;
    System.Action<IReadOnlyList<int>> onChanged;

    /// Awake가 아니라 Start에서 찾는다. MatchDirector는 자기 Awake에서 GamePhase를 잡으므로
    /// 두 Awake 사이의 순서에 기대면 `Phase`가 아직 null일 수 있다.
    void Start()
    {
        director = MatchDirector.Instance;
        if (director == null)
            Debug.LogError($"{name}: 씬에 MatchDirector가 없다. 페이즈를 알 수 없어 "
                         + "안개를 밤에만 그릴 수 없다.", this);
    }

    /// static 이벤트라 씬을 다시 열어도 구독이 남는다. 반드시 짝을 맞춘다.
    /// 전역 셰이더 값도 여기서 되돌린다 — 남겨 두면 매치를 나간 뒤 타이틀 화면까지 덮인다.
    void OnDestroy()
    {
        if (onChanged != null) FogOfWar.Changed -= onChanged;
        Shader.SetGlobalColor(FogColorProperty, Color.clear);
        Shader.SetGlobalTexture(MaskProperty, Texture2D.blackTexture);
    }

    void Update()
    {
        // 안개는 밤 전용이다 (기획서 6.1). FogOfWar.Update는 낮에 안개를 걷지 않으므로,
        // 파라미터를 그대로 두면 어젯밤 지나간 자리만 뚫린 채 자기 카페 바닥까지 덮여서
        // 낮 2분 동안 걸어도 걷히지 않는다.
        //
        // ponytail: 낮에는 알파를 0으로 눕히기만 한다. 화면 패스 자체는 계속 돈다.
        // 낮 프레임에서 전체 화면 블릿 한 번이 아까워지면 그때 Renderer Feature의
        // SetActive를 페이즈에 물린다 — 그러려면 이 어셈블리가 URP를 참조해야 한다.
        var night = director != null && director.Phase.Current == Phase.Night;
        PushParams(night ? fogColor.a : 0f);

        // 안개는 캐릭터에 붙어 있다. 로컬 플레이어가 스폰되기 전에는 그릴 것이 없고,
        // 다시 스폰되면 인스턴스가 바뀌므로 매 프레임 확인해서 다시 바인딩한다.
        var next = FogOfWar.Local();
        if (next == null) return;
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

    void PushParams(float alpha)
    {
        Shader.SetGlobalColor(FogColorProperty, new Color(fogColor.r, fogColor.g, fogColor.b, alpha));
        Shader.SetGlobalFloat(SoftnessProperty, edgeSoftness);
        Shader.SetGlobalFloat(BlurProperty, blurLevel);
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

    /// Inspector에서 값을 돌리면 곧바로 화면에 반영한다. 전역 값은 Build에서 한 번만
    /// 채우므로 이것이 없으면 플레이를 다시 시작해야 가장자리 감각을 비교할 수 있다.
    void OnValidate()
    {
        if (!Application.isPlaying) return;
        PushParams(fogColor.a);
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

        Shader.SetGlobalTexture(MaskProperty, mask);
        Shader.SetGlobalVector(TexelProperty, new Vector4(1f / side, 1f / side, side, side));

        // 월드 XZ → 마스크 UV. FogOfWar.CellIndex와 같은 식이어야 한다:
        // cell = floor(world / cellSize) + halfCells, uv = cell / Side.
        // Side는 halfCells * 2로 정의되므로 원점 보정은 항상 halfCells / Side = 0.5다.
        // 평면 메시를 쓰던 시절의 UV 180도 뒤집기 보정은 여기서 사라진다 — 월드 좌표로
        // 직접 계산하므로 메시의 UV 방향에 기대지 않는다.
        var span = side * fog.CellSize;
        Shader.SetGlobalVector(WorldToUvProperty, new Vector4(1f / span, 1f / span, 0.5f, 0.5f));
    }
}
