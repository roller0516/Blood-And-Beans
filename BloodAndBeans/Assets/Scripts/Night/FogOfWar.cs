using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// 모든 캐릭터가 하나의 안개를 공유한다 (기획서 6.1-3/6.2 "개척자의 딜레마").
/// 누가 개척하든 걷힌 칸은 전원에게 걷힌다.
///
/// 걷힌 칸 집합을 인스턴스가 아니라 프로세스 하나에 둔다. 플레이어 오브젝트마다 셋을 들면
/// SendTo.Everyone RPC라도 보낸 사람의 오브젝트 사본에서만 실행되어, 정작 내 Local()
/// 인스턴스는 비어 있는 채로 남는다.
[RequireComponent(typeof(PlayerTeam))]
public class FogOfWar : NetworkBehaviour
{
    [SerializeField] float cellSize = 1f;
    [SerializeField] int halfCells = 120;       // 격자 범위: 월드 좌표 -120~120
    [SerializeField] float revealRadius = 7f;
    [SerializeField] float sampleInterval = 0.15f;

    /// 판 전체가 공유하는 걷힌 칸. 도메인 리로드를 꺼도 이전 플레이가 새어 나오지 않도록
    /// 플레이 진입 때 비운다.
    static readonly HashSet<int> Revealed = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetShared()
    {
        Revealed.Clear();
        Changed = null;
    }

    /// 이번 샘플에서 새로 열린 칸. 매번 배열을 새로 만들지 않고 재사용한다.
    readonly List<int> pending = new();

    float nextSample;
    PlayerTeam playerTeam;
    MatchDirector director;

    /// 시야 공개 규칙이 바뀔 때마다 올린다. 낡은 스냅샷을 덮어쓰지 않기 위한 값이다.
    public const int GuardVersion = 3;

    public int TeamId => playerTeam != null ? playerTeam.Team : -1;
    public int Side => halfCells * 2;
    public float CellSize => cellSize;
    public int RevealedCount => Revealed.Count;
    public static System.Action Changed;

    void Awake() => playerTeam = GetComponent<PlayerTeam>();

    public override void OnNetworkSpawn()
    {
        // 스폰 시점에는 매치 씬이 아직 없다. 직접 캐시하면 null로 굳어 밤마다 도는
        // 안개 초기화(기획서 6.1)가 영영 걸리지 않는다.
        MatchDirector.Bind(BindDirector);

        // 늦게 합류해도 지금까지 개척된 안개를 그대로 물려받는다.
        if (IsServer) SnapshotToOwnerServer();
        Changed?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        MatchDirector.Unbind(BindDirector);
        BindDirector(null);
    }

    /// 판이 바뀌면 새 인스턴스로 다시 불린다. 시계 구독은 항상 한 곳에만 남긴다.
    void BindDirector(MatchDirector next)
    {
        if (director == next) return;
        if (director != null) director.Phase.PhaseEntered -= OnPhaseEntered;

        director = next;
        if (director != null) director.Phase.PhaseEntered += OnPhaseEntered;
    }

    /// 로컬 플레이어의 안개. 클라이언트에서는 이 인스턴스가 채워져야 한다.
    public static FogOfWar Local()
    {
        var po = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        return po != null ? po.GetComponent<FogOfWar>() : null;
    }

    /// 안개는 매일 밤 초기화된다 (기획서 6.1).
    void OnPhaseEntered(Phase p)
    {
        if (!IsServer || p != Phase.Night) return;
        FogClearRpc();
    }

    public bool IsRevealed(Vector3 world) => Revealed.Contains(CellIndex(world));

    /// 직접 셀 인덱스로 확인.
    public bool IsRevealedCell(int index) => Revealed.Contains(index);

    public int CellIndex(Vector3 world)
    {
        var x = Mathf.Clamp(Mathf.FloorToInt(world.x / cellSize) + halfCells, 0, Side - 1);
        var z = Mathf.Clamp(Mathf.FloorToInt(world.z / cellSize) + halfCells, 0, Side - 1);
        return z * Side + x;
    }

    public Vector3 CellCentre(int index)
    {
        var x = index % Side - halfCells;
        var z = index / Side - halfCells;
        return new Vector3((x + 0.5f) * cellSize, 0f, (z + 0.5f) * cellSize);
    }

    void Update()
    {
        if (!IsServer) return;

        if (director == null || director.Phase.Current != Phase.Night) return;
        if (TeamId < 0) return;

        if (Time.time < nextSample) return;
        nextSample = Time.time + sampleInterval;

        RevealAround(transform.position);
    }

    void RevealAround(Vector3 centre)
    {
        // 임대료 미납 페널티에 따라 시야 범위 축소 적용.
        var ledger = director != null ? director.LedgerOf(TeamId) : null;
        var radius = revealRadius * (ledger != null ? ledger.VisionScale : 1f);
        var steps = Mathf.CeilToInt(radius / cellSize);
        var origin = CellIndex(centre);
        var ox = origin % Side;
        var oz = origin / Side;

        pending.Clear();
        for (int dz = -steps; dz <= steps; dz++)
        for (int dx = -steps; dx <= steps; dx++)
        {
            var x = ox + dx;
            var z = oz + dz;
            if (x < 0 || z < 0 || x >= Side || z >= Side) continue;

            var idx = z * Side + x;
            if (Revealed.Contains(idx)) continue;
            if (Vector3.Distance(centre, CellCentre(idx)) > radius) continue;

            pending.Add(idx);
        }

        if (pending.Count == 0) return;
        ShareServer(pending.ToArray());
    }

    void ShareServer(int[] cells)
    {
        var changed = false;
        foreach (var cell in cells) changed |= Revealed.Add(cell);
        if (changed) Changed?.Invoke();

        // 대상은 어트리뷰트의 SendTo.Everyone이 정한다. 고정 대상 RPC에 RpcTarget을
        // 넘기면 NGO가 RpcException(Target override is not allowed)으로 막는다.
        FogCellsRpc(cells);
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    void FogCellsRpc(int[] cells, RpcParams p = default)
    {
        var changed = false;
        foreach (var cell in cells) changed |= Revealed.Add(cell);
        if (changed) Changed?.Invoke();
    }

    [Rpc(SendTo.Everyone, InvokePermission = RpcInvokePermission.Server)]
    void FogClearRpc(RpcParams p = default)
    {
        Revealed.Clear();
        Changed?.Invoke();
    }

    void SnapshotToOwnerServer() =>
        FogSnapshotRpc(new List<int>(Revealed).ToArray(),
            RpcTarget.Single(OwnerClientId, RpcTargetUse.Temp));

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    void FogSnapshotRpc(int[] cells, RpcParams p = default)
    {
        // 덮어쓰지 않고 합친다. 스냅샷과 공개 RPC는 서로 다른 오브젝트에서 오므로 도착
        // 순서가 보장되지 않고, 덮어쓰면 그 사이에 도착한 칸이 영영 사라진다.
        var changed = false;
        foreach (var cell in cells) changed |= Revealed.Add(cell);
        if (changed) Changed?.Invoke();
    }
}
