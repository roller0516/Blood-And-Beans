using UnityEngine;

/// 숲의 나무·수풀·풀을 GPU 인스턴싱으로 그린다. 씬에 GameObject를 두지 않는다.
///
/// **왜 GameObject를 버렸는가.** 씬 오브젝트로 두면 SRP Batcher가 먼저 잡아서 GPU 인스턴싱이
/// 아예 동작하지 않는다. SRP Batcher는 드로우콜을 합치는 것이 아니라 상수 버퍼 바인딩 비용만
/// 줄이므로, 같은 메시가 수천 번 반복되는 풀밭에서는 드로우콜이 그대로 수천 개다.
/// `Graphics.RenderMeshInstanced`는 렌더러를 거치지 않고 (메시, 서브메시, 머티리얼)당
/// 한 번씩 그리므로 풀 2천 포기가 드로우콜 몇 개로 끝난다.
///
/// **대가는 컬링이다.** 인스턴스마다 절두체 컬링이 걸리지 않고 배치 단위로 통째로 제출된다.
/// 이 맵은 60x60이고 카메라가 탑다운으로 대부분을 보므로 어차피 컬링될 것이 많지 않다.
/// 맵이 커지면 배치를 구역으로 쪼개서 구역 단위 컬링을 붙여야 한다.
///
/// 배치 데이터는 `ForestMapBuilder`가 굽는다. 행렬을 직렬화하지 않고 위치·회전·크기만
/// 두는 이유는 씬 파일 크기다 — Matrix4x4는 인스턴스당 64바이트라 수천 개면 씬이 부풀고
/// diff가 읽을 수 없게 된다.
[ExecuteAlways]
public class ForestInstances : MonoBehaviour
{
    /// 한 번의 RenderMeshInstanced가 받는 최대 인스턴스 수. Unity의 상한이다.
    const int MaxPerCall = 1023;

    [System.Serializable]
    public struct Batch
    {
        public Mesh Mesh;
        public int Submesh;
        public Material Material;
        public bool CastShadows;

        /// 인스턴스별 배치. 세 배열의 길이는 항상 같다.
        public Vector3[] Positions;
        public float[] Yaws;
        public float[] Scales;
    }

    [SerializeField] Batch[] batches = new Batch[0];

    /// 배치 단위 제출이라 절두체 컬링의 기준이 된다. 숲 전체를 감싸야 화면 밖으로 나갔다고
    /// 통째로 사라지지 않는다.
    [SerializeField] Bounds worldBounds = new(Vector3.zero, new Vector3(80f, 20f, 80f));

    Matrix4x4[][] matrices;
    RenderParams[] parameters;
    bool warned;

    void OnEnable()
    {
        warned = false;
        Rebuild();
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering += Submit;
    }

    void OnDisable()
    {
        UnityEngine.Rendering.RenderPipelineManager.beginCameraRendering -= Submit;
        matrices = null;
        parameters = null;
    }

    /// Inspector에서 배치를 바꾸거나 도구가 다시 구우면 곧바로 반영한다.
    void OnValidate() => Rebuild();

    /// 직렬화된 위치·회전·크기를 행렬로 편다. 매 프레임 만들면 인스턴스 수만큼 GC를 만든다.
    void Rebuild()
    {
        if (batches == null || batches.Length == 0)
        {
            matrices = null;
            parameters = null;
            return;
        }

        matrices = new Matrix4x4[batches.Length][];
        parameters = new RenderParams[batches.Length];

        for (var b = 0; b < batches.Length; b++)
        {
            var batch = batches[b];
            var count = batch.Positions != null ? batch.Positions.Length : 0;
            var built = new Matrix4x4[count];

            for (var i = 0; i < count; i++)
            {
                var yaw = batch.Yaws != null && i < batch.Yaws.Length ? batch.Yaws[i] : 0f;
                var scale = batch.Scales != null && i < batch.Scales.Length ? batch.Scales[i] : 1f;
                built[i] = Matrix4x4.TRS(batch.Positions[i], Quaternion.Euler(0f, yaw, 0f), Vector3.one * scale);
            }

            matrices[b] = built;
            parameters[b] = new RenderParams(batch.Material)
            {
                worldBounds = worldBounds,
                shadowCastingMode = batch.CastShadows
                    ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.Off,
                receiveShadows = true,
                layer = gameObject.layer,
            };
        }
    }

    /// 카메라마다 제출한다. `Update`에서 한 번 제출하는 방식은 에디터에서 그려지지 않는다 —
    /// 씬 뷰와 스크린샷 카메라는 플레이어 루프 밖에서 렌더하므로, 그때는 이미 이번 프레임의
    /// 제출이 소비된 뒤다. `RenderParams.camera`를 지정하면 그 카메라에만 그려지므로
    /// 카메라 수만큼 중복으로 그려지지도 않는다.
    ///
    /// 여기서 하는 일은 이미 만들어 둔 배열을 넘기는 것뿐이라 탐색도 할당도 없다.
    void Submit(UnityEngine.Rendering.ScriptableRenderContext context, Camera camera)
    {
        if (matrices == null || camera == null) return;

        for (var b = 0; b < batches.Length; b++)
        {
            var batch = batches[b];
            if (batch.Mesh == null || batch.Material == null) continue;

            // 인스턴싱이 꺼진 머티리얼을 넘기면 RenderMeshInstanced가 예외를 던지고,
            // 그 예외가 URP 프레임을 죽여 화면이 통째로 하얘진다. 한 번만 알리고 건너뛴다.
            if (!batch.Material.enableInstancing)
            {
                if (!warned)
                {
                    warned = true;
                    Debug.LogError($"{batch.Material.name}: GPU 인스턴싱이 꺼져 있어 숲을 "
                                 + "그리지 못한다. 머티리얼의 Enable GPU Instancing을 켠다.", this);
                }
                continue;
            }

            var rp = parameters[b];
            rp.camera = camera;

            var all = matrices[b];
            for (var start = 0; start < all.Length; start += MaxPerCall)
            {
                var count = Mathf.Min(MaxPerCall, all.Length - start);
                Graphics.RenderMeshInstanced(rp, batch.Mesh, batch.Submesh, all, count, start);
            }
        }
    }

    /// 도구가 굽는 진입점. 배치를 통째로 갈아 끼운다.
    public void SetBatches(Batch[] value, Bounds bounds)
    {
        batches = value;
        worldBounds = bounds;
        Rebuild();
    }

    public int InstanceCount
    {
        get
        {
            var total = 0;
            if (batches == null) return 0;
            foreach (var batch in batches) total += batch.Positions != null ? batch.Positions.Length : 0;
            return total;
        }
    }

    public int BatchCount => batches != null ? batches.Length : 0;
}
