using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// One team's fog. Each team explores its own darkness — a rival clearing a path
/// does not light it for you.
///
/// NOTE: design doc 6.1-3/6.2 specifies the opposite (fog shared by everyone, the
/// "개척자의 딜레마"). Per-team was directed by the project owner on 2026-08-24 and
/// overrides the document here.
///
/// Only the owning team's clients receive reveal cells.
public class FogOfWar : NetworkBehaviour
{
    /// Injected by MatchDirector, not serialized: two FogOfWar components sit on one
    /// GameObject and the team each belonged to was hidden in component order.
    int teamId = -1;
    [SerializeField] float cellSize = 1f;
    [SerializeField] int halfCells = 36;        // grid spans -36..36 world units
    [SerializeField] float revealRadius = 7f;
    [SerializeField] float sampleInterval = 0.15f;

    readonly HashSet<int> local = new();

    float nextSample;

    MatchDirector director;

    /// Bumped when the reveal guard changes, so a stale domain is easy to spot.
    public const int GuardVersion = 2;

    public int TeamId => teamId;
    public int Side => halfCells * 2;
    public float CellSize => cellSize;
    public int RevealedCount => local.Count;
    public System.Action Changed;

    /// Called by MatchDirector during Awake, on every peer.
    public void AssignTeam(int team) => teamId = team;

    public override void OnNetworkSpawn()
    {
        director = MatchDirector.Find();
        if (director != null) director.Phase.PhaseEntered += OnPhaseEntered;
        Changed?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        if (director != null) director.Phase.PhaseEntered -= OnPhaseEntered;
    }

    /// Fog resets every night (doc 6.1). The local mirror is cleared by hand because
    /// NetworkList does not raise OnListChanged back to the writer on Clear.
    void OnPhaseEntered(Phase p)
    {
        if (!IsServer || p != Phase.Night) return;
        local.Clear();
        SendClearServer();
        Changed?.Invoke();
    }

    public bool IsRevealed(Vector3 world) => local.Contains(CellIndex(world));

    /// Direct cell lookup, so the fog view can walk the grid without round-tripping
    /// every cell through a world position.
    public bool IsRevealedCell(int index) => local.Contains(index);

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

        // Cached at spawn — this was a FindFirstObjectByType every frame.
        if (director == null || director.Phase.Current != Phase.Night) return;

        if (Time.time < nextSample) return;
        nextSample = Time.time + sampleInterval;

        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            var player = client.PlayerObject;
            if (player == null) continue;

            // No team component yet means the team is not decided — revealing on a
            // guess leaked a few frames of fog into every team's map.
            var t = player.GetComponent<PlayerTeam>();
            if (t == null || t.Team != teamId) continue;   // only your own team lifts your fog

            RevealAround(player.transform.position);
        }
    }

    void RevealAround(Vector3 centre)
    {
        // Rent tier 1+ shrinks the reveal radius (doc 3.3, night column).
        var ledger = director != null ? director.LedgerOf(teamId) : null;
        var radius = revealRadius * (ledger != null ? ledger.VisionScale : 1f);
        var steps = Mathf.CeilToInt(radius / cellSize);
        var origin = CellIndex(centre);
        var ox = origin % Side;
        var oz = origin / Side;

        for (int dz = -steps; dz <= steps; dz++)
        for (int dx = -steps; dx <= steps; dx++)
        {
            var x = ox + dx;
            var z = oz + dz;
            if (x < 0 || z < 0 || x >= Side || z >= Side) continue;

            var idx = z * Side + x;
            if (local.Contains(idx)) continue;
            if (Vector3.Distance(centre, CellCentre(idx)) > radius) continue;

            local.Add(idx);
            SendCellServer(idx);
        }
    }

    public void SendSnapshotToClientServer(ulong clientId)
    {
        if (!IsServer) return;
        FogSnapshotRpc(new List<int>(local).ToArray(),
            RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    void SendCellServer(int index)
    {
        foreach (var client in NetworkManager.ConnectedClientsList)
            if (PlayerTeam.Of(client.ClientId) == teamId)
                FogCellRpc(index, RpcTarget.Single(client.ClientId, RpcTargetUse.Temp));
    }

    void SendClearServer()
    {
        foreach (var client in NetworkManager.ConnectedClientsList)
            if (PlayerTeam.Of(client.ClientId) == teamId)
                FogSnapshotRpc(System.Array.Empty<int>(),
                    RpcTarget.Single(client.ClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    void FogCellRpc(int index, RpcParams p = default)
    {
        if (local.Add(index)) Changed?.Invoke();
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    void FogSnapshotRpc(int[] cells, RpcParams p = default)
    {
        local.Clear();
        foreach (var cell in cells) local.Add(cell);
        Changed?.Invoke();
    }
}
