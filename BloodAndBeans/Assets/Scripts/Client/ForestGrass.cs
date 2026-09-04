using UnityEngine;
using UnityEngine.Rendering;

/// 숲 바닥의 풀. GPU가 위치를 뽑고 한 번의 인디렉트 드로우로 전부 그린다.
///
/// 원본: Youssef-Afella/UnityURP-InfiniteGrass (MIT). 블레이드 셰이더와 바람 텍스처는
/// 원본을 그대로 쓰고(`Assets/AssetStore/InfiniteGrass/`), 위치를 뽑는 부분만 우리 맵에 맞게
/// 줄였다. 원본의 Renderer Feature는 무한 지형을 위해 매 프레임 위에서 내려다본 높이·마스크·
/// 색·경사 텍스처 넷을 굽는데, 그 코드가 URP 14 API(`Execute`/`ConfigureTarget`)를 쓰고
/// 우리 URP 17.5에는 그 메서드가 없다. 게다가 우리 맵은 고정된 평평한 사각형이라 그 텍스처가
/// 전부 상수다 — 구울 이유가 없어서 패스를 통째로 들어냈다.
///
/// 블레이드 셰이더가 읽는 `_GrassColorRT`·`_GrassSlopeRT`에는 검은 텍스처를 물린다.
/// 그 둘의 알파가 0이면 셰이더가 "경사 없음 · 색칠 없음"으로 동작하도록 쓰여 있어서,
/// 셰이더를 고치지 않고 평지 기본값을 얻는다.
[ExecuteAlways]
public class ForestGrass : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] ComputeShader positionsCompute;
    [SerializeField] Material bladeMaterial;

    /// 밀도 마스크(R). 비워 두면 맵 전체에 가득 심는다. 길이나 빈터를 파고 싶을 때 넣는다.
    [SerializeField] Texture2D densityMask;

    [Header("맵")]
    /// 풀이 자랄 사각형. 씬의 지면과 같아야 한다 — `ForestMapBuilder`가 채운다.
    [SerializeField] Vector2 mapSize = new(60f, 60f);
    [SerializeField] Vector3 mapOrigin = Vector3.zero;

    /// 지면 높이. 중력이 없어서 어긋나면 풀이 뜬 채로 남는다.
    [SerializeField] float groundY;

    [Header("밀도")]
    /// 풀 사이 간격. 이 값이 곧 개수다 — 절반으로 줄이면 네 배가 된다.
    [SerializeField, Min(0.02f)] float spacing = 0.18f;

    /// 이 거리마다 밀도가 절반이 된다. 카메라에서 먼 풀을 솎아낸다.
    [SerializeField] float fullDensityDistance = 18f;

    /// 이보다 먼 풀은 아예 뽑지 않는다. 탑다운이라 카메라 거리(약 16)보다 넉넉하면 된다.
    [SerializeField] float drawDistance = 60f;

    [Header("블레이드 메시")]
    /// 잎 하나의 마디 수. 0이면 삼각형 하나, 늘리면 바람에 휘는 곡선이 부드러워진다.
    ///
    /// 원본 저장소는 5를 쓰지만 그쪽은 지면에 붙은 1인칭 시야를 상정한다. 우리 카메라는
    /// 16유닛 위에서 60도로 내려다보므로 잎 하나가 몇 픽셀이고 마디가 보이지 않는다.
    /// 5 -> 2로 낮추면 잎당 삼각형이 11 -> 5로 절반 이하가 되는데 화면 차이는 없다.
    [SerializeField, Range(0, 6)] int subdivision = 2;

    /// 위치 버퍼 상한. 넘치면 잘리고, 너무 크면 메모리만 먹는다.
    [SerializeField] int maxBlades = 400000;

    static readonly int GrassPositions = Shader.PropertyToID("_GrassPositions");
    static readonly int GrassColorRT = Shader.PropertyToID("_GrassColorRT");
    static readonly int GrassSlopeRT = Shader.PropertyToID("_GrassSlopeRT");

    GraphicsBuffer positions;
    GraphicsBuffer args;
    Mesh blade;
    Mesh argsMesh;
    int builtSubdivision = -1;

    void OnEnable()
    {
        RenderPipelineManager.beginCameraRendering += Submit;
    }

    void OnDisable()
    {
        RenderPipelineManager.beginCameraRendering -= Submit;
        Release();
    }

    void Release()
    {
        positions?.Release();
        positions = null;
        args?.Release();
        args = null;
        argsMesh = null;
    }

    /// 도구가 맵을 다시 구울 때 부른다.
    public void Configure(Vector3 origin, Vector2 size, float ground)
    {
        mapOrigin = origin;
        mapSize = size;
        groundY = ground;
    }

    /// 카메라마다 제출한다. 위치 추출이 카메라 위치와 절두체에 기대므로 카메라별로 돌아야
    /// 맞고, 씬 뷰처럼 플레이어 루프 밖에서 렌더하는 카메라도 이 시점에만 잡을 수 있다.
    void Submit(ScriptableRenderContext context, Camera camera)
    {
        if (camera == null || positionsCompute == null || bladeMaterial == null) return;

        // 프리뷰·프리팹 스테이지 카메라에는 그리지 않는다 (`ForestCameras`).
        if (!ForestCameras.Renders(camera, gameObject)) return;
        if (spacing <= 0f || mapSize.x <= 0f || mapSize.y <= 0f) return;

        EnsureBuffers();
        if (positions == null) return;

        // 셰이더가 알파 0을 "경사 없음 · 색칠 없음"으로 읽는다. 검은 텍스처면 평지가 된다.
        Shader.SetGlobalTexture(GrassColorRT, Texture2D.blackTexture);
        Shader.SetGlobalTexture(GrassSlopeRT, Texture2D.blackTexture);

        var min = new Vector2(mapOrigin.x - mapSize.x * 0.5f, mapOrigin.z - mapSize.y * 0.5f);
        var startIndex = new Vector2(Mathf.Floor(min.x / spacing), Mathf.Floor(min.y / spacing));
        var gridX = Mathf.CeilToInt(mapSize.x / spacing);
        var gridZ = Mathf.CeilToInt(mapSize.y / spacing);

        positions.SetCounterValue(0);

        positionsCompute.SetFloat("_Spacing", spacing);
        positionsCompute.SetFloat("_DrawDistance", drawDistance);
        positionsCompute.SetFloat("_FullDensityDistance", Mathf.Max(0.01f, fullDensityDistance));
        positionsCompute.SetFloat("_GroundY", groundY);
        positionsCompute.SetVector("_GridStartIndex", startIndex);
        positionsCompute.SetVector("_MapMin", min);
        positionsCompute.SetVector("_MapSize", mapSize);
        positionsCompute.SetVector("_CameraPosition", camera.transform.position);
        positionsCompute.SetMatrix("_VPMatrix", camera.projectionMatrix * camera.worldToCameraMatrix);
        positionsCompute.SetTexture(0, "_GrassMask", densityMask != null ? densityMask : Texture2D.blackTexture);
        positionsCompute.SetBuffer(0, GrassPositions, positions);
        positionsCompute.Dispatch(0, Mathf.CeilToInt(gridX / 8f), Mathf.CeilToInt(gridZ / 8f), 1);

        // 뽑힌 개수를 인디렉트 인자의 instanceCount(두 번째 uint)로 옮긴다.
        GraphicsBuffer.CopyCount(positions, args, sizeof(uint));

        bladeMaterial.SetBuffer(GrassPositions, positions);

        var bounds = new Bounds(mapOrigin + Vector3.up * groundY,
                                new Vector3(mapSize.x, 8f, mapSize.y));
        var rp = new RenderParams(bladeMaterial)
        {
            worldBounds = bounds,
            camera = camera,
            // 풀 그림자는 탑다운에서 거의 보이지 않는데 그림자 패스 비용은 그대로 든다.
            shadowCastingMode = ShadowCastingMode.Off,
            receiveShadows = true,
            layer = gameObject.layer,
        };

        Graphics.RenderMeshIndirect(rp, GetBlade(), args);
    }

    void EnsureBuffers()
    {
        if (positions == null)
            positions = new GraphicsBuffer(GraphicsBuffer.Target.Append, maxBlades, sizeof(float) * 3);

        var mesh = GetBlade();
        var fresh = args == null;
        if (fresh)
        {
            args = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1,
                                      GraphicsBuffer.IndirectDrawIndexedArgs.size);
        }

        // 인자는 블레이드 메시가 바뀔 때만 올린다. 매 프레임 올리면 instanceCount를 0으로
        // 덮어쓴 뒤 CopyCount가 다시 채우는 왕복이 생기고, 디버그로 개수를 읽을 때 0이 잡힌다.
        if (!fresh && argsMesh == mesh) return;

        var data = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
        data[0].indexCountPerInstance = mesh.GetIndexCount(0);
        data[0].instanceCount = 0;                    // CopyCount가 채운다
        data[0].startIndex = mesh.GetIndexStart(0);
        data[0].baseVertexIndex = mesh.GetBaseVertex(0);
        data[0].startInstance = 0;
        args.SetData(data);
        argsMesh = mesh;
    }

    /// 잎 하나짜리 메시. 원본(InfiniteGrassRenderer.GetGrassMeshCache)과 같은 모양이다 —
    /// 블레이드 셰이더가 정점의 x를 폭, y를 높이 비율로 읽기 때문에 형태를 바꾸면 셰이더가 깨진다.
    Mesh GetBlade()
    {
        if (blade != null && builtSubdivision == subdivision) return blade;

        blade = new Mesh { name = "ForestGrassBlade" };
        var vertices = new Vector3[3 + 4 * subdivision];
        var triangles = new int[(1 + 2 * subdivision) * 3];

        for (var i = 0; i < subdivision; i++)
        {
            var y1 = (float)i / (subdivision + 1);
            var y2 = (float)(i + 1) / (subdivision + 1);

            var bottomLeft = i * 4;
            var bottomRight = i * 4 + 1;
            var topLeft = i * 4 + 2;
            var topRight = i * 4 + 3;

            vertices[bottomLeft] = new Vector3(-0.25f, y1);
            vertices[bottomRight] = new Vector3(0.25f, y1);
            vertices[topLeft] = new Vector3(-0.25f, y2);
            vertices[topRight] = new Vector3(0.25f, y2);

            triangles[i * 6] = bottomLeft;
            triangles[i * 6 + 1] = topRight;
            triangles[i * 6 + 2] = bottomRight;
            triangles[i * 6 + 3] = bottomLeft;
            triangles[i * 6 + 4] = topLeft;
            triangles[i * 6 + 5] = topRight;
        }

        var tipBase = subdivision * 4;
        var tipY = (float)subdivision / (subdivision + 1);
        vertices[tipBase] = new Vector3(-0.25f, tipY);
        vertices[tipBase + 1] = new Vector3(0f, 1f);
        vertices[tipBase + 2] = new Vector3(0.25f, tipY);

        triangles[subdivision * 6] = tipBase;
        triangles[subdivision * 6 + 1] = tipBase + 1;
        triangles[subdivision * 6 + 2] = tipBase + 2;

        blade.SetVertices(vertices);
        blade.SetTriangles(triangles, 0);
        builtSubdivision = subdivision;
        return blade;
    }

    /// Inspector에서 간격이나 마디를 바꾸면 버퍼를 다시 잡아야 한다.
    void OnValidate()
    {
        builtSubdivision = -1;
        Release();
    }
}
