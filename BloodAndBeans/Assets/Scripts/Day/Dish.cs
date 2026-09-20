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
    public event System.Action CleanChanged;
    void OnClean(int previous, int current) => CleanChanged?.Invoke();
    Cafe cafe;
    void Awake() => cafe = Cafe.Of(this);
    public override void OnNetworkDespawn()
    {
        cleanCups.OnValueChanged -= OnClean; cleanPlates.OnValueChanged -= OnClean;
        if (cafe != null && cafe.Director != null) cafe.Director.Phase.PhaseEntered -= OnPhase;
    }

    /// **낮은 늘 깨끗한 잔 2 · 접시 2로 시작한다.**
    ///
    /// 기획서 5.3은 이 규칙을 적지 않는다 — 문서상 더러운 식기는 씻어야만 돌아온다.
    /// 그대로 두면 전날 서빙으로 더러워진 식기가 밤을 넘어와 다음 낮이 개수대부터
    /// 시작되고, 나흘째쯤 되면 넷 다 더러운 채로 낮이 열린다. **문서를 고치지 않기로
    /// 했으므로(2026-09-20 결정) 여기가 둘이 갈리는 유일한 자리다.**
    void OnPhase(Phase phase)
    {
        if (!IsServer || phase != Phase.Day) return;
        ResetServer();
    }

    /// 손과 보관대는 낮이 아닌 페이즈에 들어갈 때 이미 비워진다
    /// (`PlayerCarry.OnPhaseEntered` · `PrepIsland.OnPhase`). 그래서 낮 진입 시점에는
    /// 들고 있는 식기가 없고, 사용 중을 0으로 밀어도 식기가 늘어나지 않는다.
    void ResetServer()
    {
        cleanCups.Value = cups; cleanPlates.Value = plates;
        dirtyCups.Value = 0; dirtyPlates.Value = 0;
        usedCups.Value = 0; usedPlates.Value = 0;
    }
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
        if (cafe != null && cafe.Director != null) cafe.Director.Phase.PhaseEntered += OnPhase;
        if (!IsServer) return;
        ResetServer();
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
    void ReconcileServer()
    {
        Reconcile(cups, cleanCups, dirtyCups, usedCups.Value);
        Reconcile(plates, cleanPlates, dirtyPlates, usedPlates.Value);
    }
    static void Reconcile(int wanted, NetworkVariable<int> clean, NetworkVariable<int> dirty, int used)
    {
        var delta = Mathf.Max(0, wanted) - clean.Value - dirty.Value - used;
        if (delta > 0) clean.Value += delta;
        while (delta < 0 && clean.Value > 0) { clean.Value--; delta++; }
        while (delta < 0 && dirty.Value > 0) { dirty.Value--; delta++; }
    }
}
