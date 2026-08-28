using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// 숲의 전리품 박스 (기획서 6.5). 씬에 배치되므로 씬과 함께 스폰된다.
///
/// 한 상자는 *종류* 기준 5칸이다 (`LootSlots.MaxTypes`). 같은 재료는 한 칸에 쌓인다.
/// 내용물은 팀 간 선착순이지만, 개봉과 공개 진행은 사람마다 따로 돈다.
///
/// 흐름은 두 단계다.
/// 1. **개봉 캐스팅** — F를 꾹 눌러 게이지를 채운다. 서버 시계로 잰다.
/// 2. **루팅 세션** — 게이지가 차면 창이 열리고 F를 놓아도 유지된다. 칸은 1초 간격으로
///    하나씩 정체를 드러내고, 드러난 칸을 클릭하면 가방으로 통째로 들어간다.
///    이동하거나 대시에 맞으면 세션이 즉시 끊기고, 다시 열려면 캐스팅부터 시작한다.
///
/// **모든 시간 측정은 서버가 한다.** 클라이언트는 "이 박스에서 F를 누르고 있다"와
/// "이 칸을 눌렀다"만 말한다. 예전에는 소유자가 홀드를 재고 완료를 통보해서 밤 파밍
/// 루프 전체가 공짜였다 (아키텍처_v1.0.md §1.1).
public class ItemBox : NetworkBehaviour, IInteractable
{
    [SerializeField] int tier = 1;              // 1~3
    [SerializeField] float openSeconds = 0.6f;

    /// 칸 하나가 정체를 드러내는 간격 (기획서: 스킵 없이 1초 간격, 5칸이면 5초).
    [SerializeField] float revealInterval = 1f;
    [SerializeField] float reach = 2.5f;
    [SerializeField] bool temporary;            // 숲 박스가 아니라 쏟아진 더미

    /// 한 칸에 쌓이는 개수 범위. 숲 박스를 채울 때만 쓴다 — 쏟아진 더미는 실제로 들고
    /// 있던 개수를 그대로 옮긴다.
    [SerializeField] Vector2Int stackSize = new(1, 3);

    /// 어느 등급에서나 나오는 흔한 재료.
    [SerializeField] Ingredient[] commonPool =
    {
        Ingredient.Milk, Ingredient.Cream, Ingredient.Chocolate,
        Ingredient.Almond, Ingredient.Berry, Ingredient.Ice,
    };

    /// 3등급 박스에만 들어가는 중심부 보상 (기획서 6.3: 업그레이드 재료·블러드 빈).
    /// 이것이 없으면 숲 중앙까지 들어갈 이유가 없고, `BeanGrade.Blood` 가격 분기도
    /// 영영 도달하지 않는다.
    [SerializeField] Ingredient[] rarePool =
    {
        Ingredient.BloodBean, Ingredient.UpgradePart,
    };

    /// 중심부 보상이 나오기 시작하는 등급.
    [SerializeField] int rareFromTier = 3;

    /// 그 등급에서 중심부 보상이 차지하는 칸 수.
    [SerializeField] int rareSlots = 1;

    /// 서버 전용 내용물. 팀 간 선착순이라 모두가 같은 목록을 판다.
    readonly List<LootStack> stacks = new();

    /// 개봉 캐스팅. 클라이언트별로 서버 시계로 잰다.
    readonly HoldTimer hold = new();

    /// 캐스팅 중인 사람의 팀. 시작할 때 한 번 풀어 두고 틱에서는 읽기만 한다.
    readonly Dictionary<ulong, int> castTeam = new();

    /// 열려 있는 루팅 세션 하나. 개봉 시각과, 이동 취소를 판정할 이동 컴포넌트를 든다.
    ///
    /// 이동 컴포넌트를 여기 캐시하는 이유는 세션 감시가 매 프레임 돌기 때문이다. 주기 실행
    /// 안에서 컴포넌트를 조회하지 않는다 (AGENTS.md 참조와 결합도). 세션이 열리는 것은
    /// 프레임 수와 무관한 사건 한 번이라 그때 한 번만 찾는다.
    struct Session
    {
        public double OpenedAt;
        public PlayerMove Mover;
    }

    /// 개봉을 끝낸 사람의 루팅 세션. 여기 있으면 그 사람 화면에 창이 떠 있다는 뜻이다.
    readonly Dictionary<ulong, Session> sessions = new();

    readonly List<ulong> scratch = new();

    // --- 복제된 표시용 상태. 이 클라이언트의 세션 하나만 담는다 ---
    int localTier;
    int[] localItems = System.Array.Empty<int>();
    int[] localCounts = System.Array.Empty<int>();
    bool localOpened;
    double localOpenedAt;

    MatchDirector director;

    public int Tier => localTier > 0 ? localTier : tier;

    /// 이 클라이언트의 루팅 세션이 열려 있는가. 창을 여닫는 유일한 기준이다.
    public bool Opened => localOpened;

    public float Reach => reach;

    public string Prompt => Opened ? $"Tier {Tier} · 루팅 중" : $"Tier {Tier} · 길게 눌러 열기";

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

    /// 박스는 그 위의 안개가 걷힌 뒤에야 존재한다 (기획서 6.1-2). 걷힌 칸은 전원이
    /// 공유하므로(6.1-3) 보는 사람을 가리지 않는다. 방금 쏟아진 더미는 그대로 드러나 있다.
    ///
    /// `FogOfWar.Local()`을 쓰지 않는다. 서버가 로컬 플레이어의 컴포넌트로 판정하면 그것이
    /// 없는 순간(씬 전환, 전용 서버) 검사가 통째로 열린다. 게다가 매 프레임 도는 경로라
    /// 컴포넌트 조회를 둘 수도 없다 (AGENTS.md 참조와 결합도).
    public bool Cleared => temporary || FogOfWar.IsRevealedShared(transform.position);

    public override void OnNetworkSpawn()
    {
        director = MatchDirector.Instance;
        if (director != null) director.Phase.PhaseEntered += OnPhaseEntered;
        if (!IsServer) return;
        if (!temporary) ResetNightServer();
    }

    public override void OnNetworkDespawn()
    {
        if (director != null) director.Phase.PhaseEntered -= OnPhaseEntered;
        CancelAllCasts();
        sessions.Clear();
    }

    /// 쏟아진 더미는 그날 밤까지만 존재한다 (기획서 6.7). 모든 박스는 페이즈가 바뀌면
    /// 진행 중인 캐스팅과 세션을 버린다.
    void OnPhaseEntered(Phase p)
    {
        CancelAllCasts();
        if (IsServer) CloseAllSessionsServer();
        sessions.Clear();
        localOpened = false;

        if (!IsServer) return;
        if (!temporary && p == Phase.Night) { ResetNightServer(); return; }
        if (temporary && p != Phase.Night && NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
    }

    void Update()
    {
        if (!IsServer) return;

        hold.CopyClientsTo(scratch);
        for (var i = 0; i < scratch.Count; i++) TickCastServer(scratch[i]);

        PoliceSessionsServer();
    }

    /// 개봉 캐스팅 한 틱. 게이지가 다 차면 그 클라이언트의 루팅 세션이 열린다.
    void TickCastServer(ulong clientId)
    {
        if (sessions.ContainsKey(clientId)) { CancelCast(clientId); return; }

        if (director == null || director.Phase.Current != Phase.Night)
        {
            CancelCast(clientId);
            return;
        }

        // 팀은 캐스팅을 시작할 때 한 번 풀어 둔다. 매 프레임 `PlayerTeam.Of`를 부르면
        // 그 안의 `GetComponent`가 주기 실행 안의 컴포넌트 조회가 된다 (AGENTS.md).
        if (!castTeam.TryGetValue(clientId, out var team)) { CancelCast(clientId); return; }
        if (!InReach(clientId) || !Cleared) { CancelCast(clientId); return; }

        var now = NetworkManager.ServerTime.Time;

        // 임대료 페널티 2단계부터 개봉이 느려진다 (기획서 3.3). *잡고 있는 사람의* 팀으로 읽는다.
        if (!hold.TryConsume(clientId, now, RequiredSecondsFor(team))) return;

        CancelCast(clientId);

        // 쏟아진 더미는 아무것도 숨기지 않는다 (기획서 6.7). 개봉 시각을 공개가 다 끝난
        // 만큼 과거로 두면 같은 공개 식이 곧바로 전부를 드러낸다 — 분기를 만들지 않는다.
        sessions[clientId] = new Session
        {
            OpenedAt = temporary ? now - revealInterval * stacks.Count : now,
            Mover = MoverOf(clientId),
        };
        SendSessionStateServer(clientId);
    }

    /// 세션이 유지될 조건을 계속 확인한다. 자리를 뜨거나 밤이 끝나면 창이 닫힌다.
    /// 이동 취소는 여기서 본다 — 클라이언트가 "안 움직였다"고 말하게 두면 파밍 캔슬이
    /// 그냥 없는 규칙이 된다.
    void PoliceSessionsServer()
    {
        if (sessions.Count == 0) return;

        scratch.Clear();
        foreach (var pair in sessions) scratch.Add(pair.Key);

        var night = director != null && director.Phase.Current == Phase.Night;
        for (var i = 0; i < scratch.Count; i++)
        {
            var clientId = scratch[i];
            var mover = sessions[clientId].Mover;

            // 이동 컴포넌트가 사라졌으면 그 플레이어가 없어진 것이다. 이동하지 않은 것으로
            // 보고 세션을 남기면 주인 없는 창이 밤 내내 떠 있는다.
            if (night && mover != null && !mover.MovingServer && InReach(clientId)) continue;
            CancelSessionServer(clientId);
        }
    }

    /// 캐스팅을 버린다. 타이머와 캐시한 팀은 항상 함께 산다 — 한쪽만 지우면 다음 캐스팅이
    /// 남의 팀 값이나 사라진 값으로 개봉 시간을 계산한다.
    void CancelCast(ulong clientId)
    {
        hold.Cancel(clientId);
        castTeam.Remove(clientId);
    }

    void CancelAllCasts()
    {
        hold.CancelAll();
        castTeam.Clear();
    }

    static PlayerMove MoverOf(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.ConnectedClients.TryGetValue(clientId, out var c)) return null;
        return c.PlayerObject != null ? c.PlayerObject.GetComponent<PlayerMove>() : null;
    }

    /// 드러난 칸 하나를 통째로 가방에 넣는다 (기획서: 클릭하면 팀 인벤토리로 바로 이동).
    /// 클라이언트가 보낸 인덱스는 전부 서버에서 다시 검사한다.
    public void TakeStackServer(ulong clientId, int index)
    {
        if (!IsServer || !sessions.TryGetValue(clientId, out var session)) return;
        if (index < 0 || index >= stacks.Count) return;
        if (index >= LootSlots.RevealedCount(
                NetworkManager.ServerTime.Time, session.OpenedAt, revealInterval, stacks.Count)) return;

        var stack = stacks[index];
        if (stack.Count <= 0) return;

        // 가방을 받은 뒤에야 칸을 비운다. 반대로 하면 가방을 묻어 둔 채 칸을 누르는 것만으로
        // 재료가 어디에도 없이 사라지고, 내용물이 팀 간 선착순이라 남의 몫까지 같이 없어진다.
        var inv = InventoryOf(clientId);
        if (inv == null || !inv.AddServer(stack.Item, stack.Count)) return;

        stacks[index] = new LootStack(stack.Item, 0);

        // 다 털린 임시 더미는 그 자리에서 치운다. 남겨 두면 빈 상자가 밤이 끝날 때까지
        // 서서 아직 뭔가 있는 것처럼 보인다. 숲 박스는 밤마다 다시 채워지므로 남긴다.
        if (temporary && Empty)
        {
            CloseAllSessionsServer();
            if (NetworkObject != null && NetworkObject.IsSpawned) NetworkObject.Despawn();
            return;
        }

        // 내용물은 팀 간 선착순이다. 같은 상자를 보고 있는 모두의 화면에서 그 칸이 비어야 한다.
        SendAllSessionsStateServer();
    }

    /// 서버에서 PlayerInteract가 호출한다. 안개와 거리를 여기서 검사해야 클라이언트가
    /// 근처에도 없는 박스에 캐스팅을 등록하지 못한다.
    public void BeginHoldServer(ulong clientId)
    {
        if (!IsServer) return;
        if (director == null || director.Phase.Current != Phase.Night) return;
        if (sessions.ContainsKey(clientId)) return;           // 이미 열려 있다

        var team = PlayerTeam.Of(clientId);
        if (team < 0 || !InReach(clientId) || !Cleared) return;

        castTeam[clientId] = team;
        hold.Begin(clientId, NetworkManager.ServerTime.Time);
    }

    /// F를 놓았다. 캐스팅만 버린다 — 이미 열린 루팅 세션은 F와 무관하게 유지된다.
    public void EndHoldServer(ulong clientId)
    {
        if (IsServer) CancelCast(clientId);
    }

    /// 캐스팅과 세션을 모두 버린다. 다시 열려면 캐스팅부터 시작해야 한다.
    public void CancelSessionServer(ulong clientId)
    {
        if (!IsServer) return;
        CancelCast(clientId);
        if (!sessions.Remove(clientId)) return;
        SendSessionStateServer(clientId);                     // 닫힘을 알린다
    }

    /// 서버 전용. 남은 칸이 하나도 없는가.
    bool Empty
    {
        get
        {
            for (var i = 0; i < stacks.Count; i++) if (stacks[i].Count > 0) return false;
            return true;
        }
    }

    void CloseAllSessionsServer()
    {
        scratch.Clear();
        foreach (var pair in sessions) scratch.Add(pair.Key);
        for (var i = 0; i < scratch.Count; i++) CancelSessionServer(scratch[i]);
    }

    /// 표시 전용. 권위 있는 진행도는 서버의 `hold`에 있다. 잡고 있는 본인에게 자기
    /// 경과 시간을 보여 주는 것은 복제할 필요가 없다.
    public float RequiredSecondsFor(int team)
    {
        var ledger = director != null ? director.LedgerOf(team) : null;
        return openSeconds * (ledger != null ? ledger.BoxOpenScale : 1f);
    }

    /// 쏟아진 그대로를 담아 더미를 만든다. 종류가 넘치면 상자를 쪼개는 것은 호출자의
    /// 몫이다 (`LootSlots.Pack`).
    public void SeedServer(IEnumerable<LootStack> contents)
    {
        if (!IsServer) return;

        stacks.Clear();
        foreach (var c in contents)
        {
            if (stacks.Count >= LootSlots.MaxTypes) break;     // 쪼개는 것은 호출자의 몫이다
            if (c.Count > 0) stacks.Add(c);
        }
    }

    void Fill()
    {
        stacks.Clear();

        // 등급이 칸 수를 정한다 (기획서 6.5.2). 고정이 아니라 범위라 같은 등급이라도
        // 밤마다 달라진다.
        LootSlots.SlotRangeFor(tier, out var minTypes, out var maxTypes);
        var types = Random.Range(minTypes, maxTypes + 1);

        // 3등급이면 중심부 보상을 먼저 몇 칸 채우고 나머지를 흔한 재료로 메운다.
        var rare = tier >= rareFromTier ? Mathf.Clamp(rareSlots, 0, types) : 0;
        DrawInto(rarePool, rare);
        DrawInto(commonPool, types - stacks.Count);
    }

    /// 풀에서 서로 다른 종류를 `count`칸만큼 뽑는다. 같은 종류가 두 칸이 되면 안 된다 —
    /// 칸 제한이 개수가 아니라 종류 기준이기 때문이다 (`LootSlots.MaxTypes`).
    void DrawInto(Ingredient[] pool, int count)
    {
        if (pool == null || count <= 0) return;

        var remaining = new List<Ingredient>(pool);
        for (var i = 0; i < count && remaining.Count > 0; i++)
        {
            var pick = Random.Range(0, remaining.Count);
            stacks.Add(new LootStack(remaining[pick], Random.Range(stackSize.x, stackSize.y + 1)));
            remaining.RemoveAt(pick);
        }
    }

    void ResetNightServer()
    {
        if (!IsServer || temporary) return;
        tier = Random.Range(1, 4);
        CloseAllSessionsServer();
        Fill();
    }

    // --- 클라이언트 표시 ---

    public int SlotCount => localItems.Length;

    public Ingredient SlotItem(int index) =>
        index < 0 || index >= localItems.Length ? Ingredient.None : (Ingredient)localItems[index];

    public int SlotCountAt(int index) =>
        index < 0 || index >= localCounts.Length ? 0 : localCounts[index];

    /// 서버와 같은 식을 쓴다 (`LootSlots.RevealedCount`). 공개는 시각 하나로 결정되므로
    /// 칸이 드러날 때마다 RPC를 보내지 않는다.
    public int RevealedCount => localOpened
        ? LootSlots.RevealedCount(
            NetworkManager.ServerTime.Time, localOpenedAt, revealInterval, localItems.Length)
        : 0;

    public bool IsSlotRevealed(int index) => index >= 0 && index < RevealedCount;

    /// 접속 직후 한 번. 이 클라이언트에는 아직 세션이 없으므로 닫힌 상태가 간다.
    public void SendStateToClientServer(ulong clientId, int team)
    {
        if (IsServer) SendSessionStateServer(clientId);
    }

    void SendSessionStateServer(ulong clientId)
    {
        if (!IsServer) return;

        var open = sessions.TryGetValue(clientId, out var session);
        var items = System.Array.Empty<int>();
        var counts = System.Array.Empty<int>();

        if (open)
        {
            items = new int[stacks.Count];
            counts = new int[stacks.Count];
            for (var i = 0; i < stacks.Count; i++)
            {
                items[i] = (int)stacks[i].Item;
                counts[i] = stacks[i].Count;
            }
        }

        BoxStateRpc(tier, open, session.OpenedAt, items, counts,
            RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    void SendAllSessionsStateServer()
    {
        scratch.Clear();
        foreach (var pair in sessions) scratch.Add(pair.Key);
        for (var i = 0; i < scratch.Count; i++) SendSessionStateServer(scratch[i]);
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    void BoxStateRpc(int shownTier, bool isOpened, double openedAt, int[] items, int[] counts,
        RpcParams p = default)
    {
        localTier = shownTier;
        localOpened = isOpened;
        localOpenedAt = openedAt;
        localItems = items ?? System.Array.Empty<int>();
        localCounts = counts ?? System.Array.Empty<int>();
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
