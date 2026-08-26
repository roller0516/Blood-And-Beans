using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// 숲의 전리품 박스 (기획서 6.5). 씬에 배치되므로 씬과 함께 스폰된다.
///
/// 내용물은 선착순이지만 개봉 여부와 공개 상태는 팀별로 분리된다.
///
/// **모든 시간 측정은 서버가 한다.** 클라이언트는 "이 박스에서 F를 누르고 있다"만 말하고,
/// 개봉과 담기는 *서버* 시계가 홀드 시간이 채워졌다고 판단할 때 일어난다. 예전에는 소유자가
/// 홀드를 재고 완료를 통보해서 밤 파밍 루프 전체가 공짜였다 (아키텍처_v1.0.md §1.1).
public class ItemBox : NetworkBehaviour, IInteractable
{
    [SerializeField] int tier = 1;              // 1~3
    [SerializeField] float openSeconds = 0.6f;
    [SerializeField] float takeSeconds = 0.2f;
    [SerializeField] float revealDelay = 1.5f;  // 이 시간이 지나면 가려진 칸이 드러난다
    [SerializeField] float reach = 2.5f;
    [SerializeField] bool temporary;            // 숲 박스가 아니라 쏟아진 더미

    readonly List<int> slots = new();             // 서버 전용
    bool[] openedByTeam = System.Array.Empty<bool>();
    double[] revealAtByTeam = System.Array.Empty<double>();

    int localTier;
    int[] localSlots = System.Array.Empty<int>();
    bool localOpened;
    double localRevealAt;

    readonly HoldTimer hold = new();
    readonly List<ulong> holders = new();

    /// 잡고 있는 사람이 고른 칸 (기획서 6.5.1). 서버 전용이며 홀드가 끝나면 지운다.
    readonly Dictionary<ulong, int> selectedByClient = new();

    MatchDirector director;

    public int Tier => localTier > 0 ? localTier : tier;
    public bool Opened => localOpened;
    public float Reach => reach;
    public string Prompt => Opened
        ? $"Tier {Tier} · {RemainingCount}/{SlotCount}" + (Revealed ? "" : " · 공개 중")
        : $"Tier {Tier} · 길게 눌러 열기";

    public void BeginInteractionClient()
    {
        var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        player?.GetComponent<PlayerInteract>()?.BeginBoxClient(this);
    }

    public void EndInteractionClient()
    {
        var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
        player?.GetComponent<PlayerInteract>()?.EndBoxClient();
    }

    /// 박스는 그 위의 안개가 걷힌 뒤에야 플레이어에게 존재한다 (기획서 6.1-2).
    /// 안개가 팀별이므로 "이 박스가 보이는가"는 보는 사람을 지정해야만 의미가 있다.
    /// 방금 쏟아진 더미는 어느 쪽이든 그대로 드러나 있다.
    public bool ClearedFor(int team)
    {
        if (temporary) return true;
        var f = FogOfWar.Local();
        return f == null || f.IsRevealed(transform.position);
    }

    /// 이 인덱스 뒤의 칸은 가려진 채로 시작해 타이머가 지나면 드러난다 (기획서 6.5.2).
    int VisibleCount => Tier switch { 1 => localSlots.Length, 2 => localSlots.Length - 1, _ => 2 };

    public bool Revealed =>
        localOpened && NetworkManager.ServerTime.Time >= localRevealAt;

    public override void OnNetworkSpawn()
    {
        director = MatchDirector.Instance;
        if (director != null) director.Phase.PhaseEntered += OnPhaseEntered;
        if (!IsServer) return;
        openedByTeam = new bool[director != null ? director.TeamCount : 1];
        revealAtByTeam = new double[openedByTeam.Length];
        if (!temporary) ResetNightServer();
    }

    public override void OnNetworkDespawn()
    {
        if (director != null) director.Phase.PhaseEntered -= OnPhaseEntered;
        hold.CancelAll();
        selectedByClient.Clear();
    }

    /// 쏟아진 더미는 그날 밤까지만 존재한다 (기획서 6.7). 모든 박스는 페이즈가 바뀌면
    /// 진행 중인 홀드를 버린다. 홀드가 시작된 밤보다 오래 살아남으면 안 된다.
    void OnPhaseEntered(Phase p)
    {
        hold.CancelAll();
        selectedByClient.Clear();
        if (!IsServer) return;
        if (!temporary && p == Phase.Night) { ResetNightServer(); return; }
        if (temporary && p != Phase.Night && NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
    }

    void Update()
    {
        if (!IsServer) return;

        hold.CopyClientsTo(holders);
        for (var i = 0; i < holders.Count; i++) Tick(holders[i]);
    }

    /// 키 하나를 계속 누르는 동안: 개봉 시간을 한 번 치르고, 그 뒤로 담기 시간마다 하나씩.
    void Tick(ulong clientId)
    {
        if (director == null || director.Phase.Current != Phase.Night)
        {
            hold.Cancel(clientId);
            return;
        }
        var team = PlayerTeam.Of(clientId);
        if (!InReach(clientId) || !ClearedFor(team)) { hold.Cancel(clientId); return; }

        var now = NetworkManager.ServerTime.Time;

        if (!OpenedFor(team))
        {
            // 임대료 페널티 2단계부터 개봉이 느려진다 (기획서 3.3). *잡고 있는 사람의* 팀으로 읽는다.
            if (!hold.TryConsume(clientId, now, RequiredSecondsFor(team))) return;

            openedByTeam[team] = true;
            revealAtByTeam[team] = now + revealDelay;
            SendTeamStateServer(team);
            return;                              // 담기 시계는 여기서부터 시작한다
        }

        if (hold.TryConsume(clientId, now, takeSeconds)) TakeOne(clientId, team);
    }

    /// 고른 칸을 가져간다 (기획서 6.5.1). 고르지 않았거나 고른 칸이 담을 수 없게 됐으면
    /// 담을 수 있는 첫 칸으로 되돌린다 — 내용물은 팀 간 선착순이라 고른 칸이 남의 손에
    /// 사라질 수 있고, 그때 홀드가 아무 일도 안 하면 원인을 알 수 없다.
    void TakeOne(ulong clientId, int team)
    {
        var inv = InventoryOf(clientId);
        if (inv == null) return;

        selectedByClient.TryGetValue(clientId, out var selected);
        var index = EffectiveSlotServer(selected, team);
        if (index < 0) return;                    // 남은 칸이 아직 하나도 안 드러났다

        inv.AddServer((Ingredient)slots[index]);
        slots[index] = (int)Ingredient.None;
        SendOpenedTeamsServer();
    }

    /// 서버에서 PlayerInteract가 호출한다. 값 검사는 `EffectiveSlotServer`가 하므로
    /// 범위를 벗어난 값이 와도 담을 수 있는 첫 칸으로 떨어질 뿐이다.
    public void SelectSlotServer(ulong clientId, int index)
    {
        if (!IsServer) return;
        selectedByClient[clientId] = index;
    }

    /// 서버 권위. 이 팀이 지금 이 칸을 담을 수 있는가.
    bool IsTakableFor(int index, int team) =>
        index >= 0 && index < slots.Count &&
        slots[index] != (int)Ingredient.None && IsSlotVisibleFor(index, team);

    int EffectiveSlotServer(int selected, int team)
    {
        if (IsTakableFor(selected, team)) return selected;
        for (var i = 0; i < slots.Count; i++)
            if (IsTakableFor(i, team)) return i;
        return -1;
    }

    /// 서버에서 PlayerInteract가 호출한다. 안개와 거리를 여기서 검사해야 클라이언트가
    /// 근처에도 없는 박스에 홀드를 등록하지 못한다.
    public void BeginHoldServer(ulong clientId)
    {
        if (!IsServer) return;
        if (director == null || director.Phase.Current != Phase.Night) return;
        if (!InReach(clientId) || !ClearedFor(PlayerTeam.Of(clientId))) return;
        hold.Begin(clientId, NetworkManager.ServerTime.Time);
    }

    public void CancelHoldServer(ulong clientId)
    {
        if (!IsServer) return;
        hold.Cancel(clientId);
        selectedByClient.Remove(clientId);
    }

    /// 대시를 맞으면 진행 중인 개봉이 끊기지만 절반은 남는다 (기획서 6.6).
    public void HalveHoldServer(ulong clientId)
    {
        if (!IsServer) return;
        hold.Halve(clientId, NetworkManager.ServerTime.Time);
    }

    /// 표시 전용. 권위 있는 진행도는 서버의 `hold`에 있다. 잡고 있는 본인에게 자기
    /// 경과 시간을 보여 주는 것은 복제할 필요가 없다.
    public float RequiredSecondsFor(int team)
    {
        if (IsServer ? OpenedFor(team) : localOpened) return takeSeconds;
        var ledger = director != null ? director.LedgerOf(team) : null;
        return openSeconds * (ledger != null ? ledger.BoxOpenScale : 1f);
    }

    /// 쏟아진 그대로를 담아 더미를 만든다. 이미 열린 상태다. 떨어뜨린 가방은 아무것도
    /// 숨기지 않는다 (기획서 6.7).
    public void SeedServer(IEnumerable<Ingredient> contents)
    {
        if (!IsServer) return;

        slots.Clear();
        foreach (var c in contents) slots.Add((int)c);
        for (var team = 0; team < openedByTeam.Length; team++) openedByTeam[team] = true;
        SendAllClientsStateServer(0d);
    }

    void Fill()
    {
        slots.Clear();
        var count = tier switch { 1 => 3, 2 => 4, _ => 5 };
        for (var i = 0; i < count; i++) slots.Add((int)RollForTier());
    }

    void ResetNightServer()
    {
        if (!IsServer || temporary) return;
        tier = Random.Range(1, 4);
        System.Array.Clear(openedByTeam, 0, openedByTeam.Length);
        System.Array.Clear(revealAtByTeam, 0, revealAtByTeam.Length);
        Fill();
        for (var team = 0; team < openedByTeam.Length; team++) SendTeamStateServer(team);
    }

    Ingredient RollForTier()
    {
        var common = new[]
        {
            Ingredient.Milk, Ingredient.Cream, Ingredient.Chocolate,
            Ingredient.Almond, Ingredient.Berry, Ingredient.Ice,
        };
        return common[Random.Range(0, common.Length)];
    }

    bool OpenedFor(int team) => team >= 0 && team < openedByTeam.Length && openedByTeam[team];

    bool IsSlotVisibleFor(int index, int team)
    {
        var visibleCount = tier switch { 1 => slots.Count, 2 => slots.Count - 1, _ => 2 };
        return index < visibleCount ||
            (OpenedFor(team) && NetworkManager.ServerTime.Time >= revealAtByTeam[team]);
    }

    /// 애초에 가려지지 않았거나 타이머가 끝났으면 클라이언트에게 보인다.
    public bool IsSlotVisible(int index) => index < VisibleCount || Revealed;

    /// 표시 전용. 서버의 `IsTakableFor`와 같은 규칙을 복제된 상태로 본다. 담을 칸을
    /// 정하는 것은 서버이고, 클라이언트는 어디에 커서를 그릴지에만 이 값을 쓴다.
    public bool IsTakable(int index) =>
        index >= 0 && index < localSlots.Length &&
        localSlots[index] != (int)Ingredient.None && IsSlotVisible(index);

    /// 표시 전용. 서버의 `EffectiveSlotServer`와 같은 되돌림 규칙이다.
    public int EffectiveSlot(int selected)
    {
        if (IsTakable(selected)) return selected;
        for (var i = 0; i < localSlots.Length; i++)
            if (IsTakable(i)) return i;
        return -1;
    }

    public Ingredient SlotContent(int index) =>
        index < 0 || index >= localSlots.Length ? Ingredient.None : (Ingredient)localSlots[index];

    public int SlotCount => localSlots.Length;

    public int RemainingCount
    {
        get
        {
            var n = 0;
            foreach (var s in localSlots) if (s != (int)Ingredient.None) n++;
            return n;
        }
    }

    public void SendStateToClientServer(ulong clientId, int team)
    {
        if (!IsServer || team < 0 || team >= openedByTeam.Length) return;
        var contents = openedByTeam[team] ? slots.ToArray() : System.Array.Empty<int>();
        BoxStateRpc(tier, openedByTeam[team], revealAtByTeam[team], contents,
            RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    void SendTeamStateServer(int team)
    {
        if (!IsServer || team < 0 || team >= openedByTeam.Length) return;
        foreach (var client in NetworkManager.ConnectedClientsList)
            if (PlayerTeam.Of(client.ClientId) == team)
                SendStateToClientServer(client.ClientId, team);
    }

    void SendOpenedTeamsServer()
    {
        for (var team = 0; team < openedByTeam.Length; team++)
            if (openedByTeam[team]) SendTeamStateServer(team);
    }

    void SendAllClientsStateServer(double revealAt)
    {
        foreach (var client in NetworkManager.ConnectedClientsList)
            BoxStateRpc(tier, true, revealAt, slots.ToArray(),
                RpcTarget.Single(client.ClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    void BoxStateRpc(int shownTier, bool isOpened, double shownRevealAt, int[] contents,
        RpcParams p = default)
    {
        localTier = shownTier;
        localOpened = isOpened;
        localRevealAt = shownRevealAt;
        localSlots = contents ?? System.Array.Empty<int>();
    }

    bool InReach(ulong clientId)
    {
        var t = Station.PlayerOf(clientId);
        return t != null && Vector3.Distance(t.position, transform.position) <= reach;
    }

    PlayerInventory InventoryOf(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.ConnectedClients.TryGetValue(clientId, out var c)) return null;
        return c.PlayerObject != null ? c.PlayerObject.GetComponent<PlayerInventory>() : null;
    }
}
