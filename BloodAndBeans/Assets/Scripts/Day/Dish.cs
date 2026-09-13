using Unity.Netcode;
using UnityEngine;

/// 팀 소유 잔·접시를 별도로 센다 (기획서 5.3). 조리 중인 식기는 만료 때 빼앗지 않는다.
public class Dish : NetworkBehaviour
{
    // ponytail: 14장 #5 미결. 잔 2개·접시 2개로 시작하며 확정 시 기본 수량을 조정한다.
    [SerializeField] int cups = 2;
    [SerializeField] int plates = 2;
    readonly NetworkVariable<int> cleanCups = new();
    readonly NetworkVariable<int> cleanPlates = new();
    readonly NetworkVariable<int> dirtyCups = new();
    readonly NetworkVariable<int> dirtyPlates = new();
    readonly NetworkVariable<int> usedCups = new();
    readonly NetworkVariable<int> usedPlates = new();
    bool buff;
    bool broken;
    public event System.Action CleanChanged;
    void OnClean(int previous, int current) => CleanChanged?.Invoke();
    public override void OnNetworkDespawn()
    { cleanCups.OnValueChanged -= OnClean; cleanPlates.OnValueChanged -= OnClean; }
    public int Clean => cleanCups.Value + cleanPlates.Value;
    public int Dirty => dirtyCups.Value + dirtyPlates.Value;
    public int InUse => usedCups.Value + usedPlates.Value;
    public int CleanCups => cleanCups.Value;
    public int CleanPlates => cleanPlates.Value;
    public bool TakeDirtyServer(out bool plate)
    {
        plate = dirtyCups.Value <= 0;
        var dirty = plate ? dirtyPlates : dirtyCups;
        if (!IsServer || dirty.Value <= 0) return false;
        dirty.Value--;
        if (plate) usedPlates.Value++; else usedCups.Value++;
        return true;
    }
    public override void OnNetworkSpawn()
    {
        cleanCups.OnValueChanged += OnClean; cleanPlates.OnValueChanged += OnClean;
        if (!IsServer) return;
        cleanCups.Value = cups; cleanPlates.Value = plates;
    }
    public bool ClaimServer(bool plate = false)
    {
        var clean = plate ? cleanPlates : cleanCups;
        if (!IsServer || clean.Value <= 0) return false;
        clean.Value--;
        if (plate) usedPlates.Value++; else usedCups.Value++;
        return true;
    }
    public void SoilServer(bool plate = false)
    {
        var used = plate ? usedPlates : usedCups;
        if (!IsServer || used.Value <= 0) return;
        used.Value--;
        if (plate) dirtyPlates.Value++; else dirtyCups.Value++;
        ReconcileServer();
    }
    public void WashServer()
    {
        if (!IsServer) return;
        if (dirtyCups.Value > 0) { dirtyCups.Value--; cleanCups.Value++; }
        else if (dirtyPlates.Value > 0) { dirtyPlates.Value--; cleanPlates.Value++; }
    }
    public void ApplyBuffServer(bool value) { if (IsServer) { buff = value; ReconcileServer(); } }
    public void SetBreakageServer(bool value) { if (IsServer) { broken = value; ReconcileServer(); } }
    void ReconcileServer()
    {
        Reconcile(cups + (buff ? 1 : 0) - (broken ? 1 : 0), cleanCups, dirtyCups, usedCups.Value);
        Reconcile(plates + (buff ? 1 : 0), cleanPlates, dirtyPlates, usedPlates.Value);
    }
    static void Reconcile(int wanted, NetworkVariable<int> clean, NetworkVariable<int> dirty, int used)
    {
        var delta = Mathf.Max(0, wanted) - clean.Value - dirty.Value - used;
        if (delta > 0) clean.Value += delta;
        while (delta < 0 && clean.Value > 0) { clean.Value--; delta++; }
        while (delta < 0 && dirty.Value > 0) { dirty.Value--; delta++; }
    }
}
