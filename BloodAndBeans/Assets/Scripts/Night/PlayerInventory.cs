using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// 밤에 들고 다니는 가방 (기획서 6.7). 개인 인벤토리는 없다 — 여기 담긴 것은 전부
/// 팀의 것이고, 귀환하면 그대로 팀 재고가 된다 (`ReturnZone`).
///
/// 칸 제한도 무게 제한도 없다. 얼마든지 더 담을 수 있고, 대신 기어간다. 무게가 일정
/// 비율을 넘으면 대시도 못 쓴다 (`DashHarass`).
///
/// 아이템을 총합 숫자가 아니라 개별로 추적한다. 기획서 6.6이 적재분의 일부를 주울 수 있는
/// 더미로 바닥에 흘리게 하는데, 기록하지 않은 것은 흘릴 수 없기 때문이다.
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(PlayerMove))]
public class PlayerInventory : NetworkBehaviour
{
    [SerializeField] float capacity = 20f;
    [SerializeField] ItemBox pilePrefab;

    /// 땅에 묻는 가방. 비워야 대시를 쓸 수 있으므로 기동성과 맞바꾸는 선택이다.
    [SerializeField] BuriedBag buriedBagPrefab;

    /// 쪼개진 임시 상자를 벌려 놓는 간격. 겹쳐 놓으면 하나만 집을 수 있다.
    [SerializeField] float pileSpacing = 1.2f;

    readonly NetworkList<int> items = new(null,
        NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<float> carried = new(0f,
        NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

    /// 가방을 지금 메고 있는가. 묻어 두면 false다. 밤이 끝날 때 이 값이 정산을 가른다
    /// (가방 미소지는 소환 위치와 무관하게 전량 소실).
    readonly NetworkVariable<bool> hasBag = new(true,
        NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

    /// 적재가 80%를 넘었는가 (기획서 6.6).
    ///
    /// **이 한 비트만 전원에게 공개된다.** 무게도 내용물도 소유자만 읽지만(`carried`,
    /// `items`), 이것은 감출 수 없다 — 기획서가 "적재 80%를 넘긴 캐릭터는 겉보기에도
    /// 표시된다"고 정했고, 보이지 않으면 80% 낙하 규칙을 노리고 대시할 방법이 없어
    /// 견제 설계가 통째로 작동하지 않는다.
    readonly NetworkVariable<bool> overloaded = new(false,
        NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    MatchDirector director;
    GamePhase subscribedPhase;

    // 흘린 더미를 발밑에 맞출 때 캡슐 치수가 필요하다. 스폰 시점에 한 번만 찾는다.
    CharacterController controller;

    PlayerTeam team;

    // 무게가 바뀔 때마다 속도 배수를 여기로 민다. 이동이 매 프레임 원장을 뒤지지 않게
    // 하기 위해서다 - 무게는 재료를 담고 버릴 때만 바뀌지 프레임마다 바뀌지 않는다.
    PlayerMove move;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        team = GetComponent<PlayerTeam>();
        move = GetComponent<PlayerMove>();
    }

    // 스폰 시점에는 매치 씬이 아직 없다. 직접 캐시하면 null로 굳어 임대료 페널티의
    // 무게 밴드 이동(기획서 3.3)이 적용되지 않는다.
    public override void OnNetworkSpawn()
    {
        MatchDirector.Bind(BindDirector);

        // 겉보기는 서버·클라이언트 양쪽에서 그린다. 남의 적재 상태도 보여야 하므로
        // 소유자 분기를 두지 않는다 (기획서 6.6).
        overloaded.OnValueChanged += OnOverloadedChanged;
        LoadChanged?.Invoke();

        if (!IsServer) return;

        // 담고 버리는 경로가 여럿이라(AddServer, ClearServer, DumpRpc, 흘리기) 한 곳씩
        // 찾아 붙이면 언젠가 하나를 빠뜨린다. 값이 바뀌는 지점 하나만 본다.
        carried.OnValueChanged += OnCarriedChangedServer;
        PushSpeedServer();
    }

    public override void OnNetworkDespawn()
    {
        MatchDirector.Unbind(BindDirector);
        overloaded.OnValueChanged -= OnOverloadedChanged;
        if (IsServer) carried.OnValueChanged -= OnCarriedChangedServer;
        if (subscribedPhase != null) subscribedPhase.PhaseEntered -= OnPhaseEntered;
        subscribedPhase = null;
    }

    void OnCarriedChangedServer(float previous, float current) => PushSpeedServer();

    void OnOverloadedChanged(bool _, bool __) => LoadChanged?.Invoke();

    /// 지금 무게와 밴드로 정해지는 속도 배수를 이동에 넣는다.
    ///
    /// 미는 쪽이 여기인 이유: 배수는 원장(서버 전용)과 무게에서 나오는데 둘 다 이 컴포넌트
    /// 쪽 사정이다. 이동이 이걸 물어보게 두면 이동이 가방과 임대료를 알아야 한다.
    void PushSpeedServer()
    {
        if (!IsServer) return;
        move.SetSpeedScaleServer(CurrentSpeedMultiplier);

        // 겉보기 판정도 같은 자리에서 민다. 무게가 바뀌는 지점이 여기 하나뿐이라
        // (`OnCarriedChangedServer`) 둘이 갈라질 여지가 없다.
        overloaded.Value = hasBag.Value && LoadRatio >= LoadBands.OverloadRatio;
    }

    /// 같은 인스턴스로 두 번 불려도 되게 짠다 (`MatchDirector.Bind` 계약).
    void BindDirector(MatchDirector next)
    {
        director = next;
        var phase = next != null ? next.Phase : null;
        if (phase == subscribedPhase) return;

        if (subscribedPhase != null) subscribedPhase.PhaseEntered -= OnPhaseEntered;
        subscribedPhase = phase;
        if (subscribedPhase != null) subscribedPhase.PhaseEntered += OnPhaseEntered;

        // 원장이 이제야 잡혔다. 밴드가 옮겨져 있으면 지금 값이 달라진다.
        PushSpeedServer();
    }

    /// 가방을 잃어버리거나 소각당했더라도 다음 밤이 시작되면 다시 기본 지급된다.
    void OnPhaseEntered(Phase p)
    {
        if (!IsServer || p != Phase.Night) return;
        ClearServer();
        hasBag.Value = true;
        BuriedLossCount = 0;    // 새 밤이다 — 지난 밤에 묻어 두고 못 찾은 것은 이미 정산됐다

        // 임대료 페널티는 정산에서 정해진다. 무게가 그대로여도 밴드가 한 칸 옮겨져 있을
        // 수 있으므로 밤이 시작될 때 한 번 다시 넣는다 (기획서 3.3).
        PushSpeedServer();
    }

    public float Carried => carried.Value;

    /// 표시 전용. 가방 용량(KG). 적재량을 비율만이 아니라 절대값으로도 보여 준다.
    public float Capacity => capacity;
    public float LoadRatio => carried.Value / capacity;
    public int Count => items.Count;

    /// 가방을 메고 있는가. 묻어 둔 동안에는 아무것도 담을 수 없고 무게도 0이다.
    public bool HasBag => hasBag.Value;

    /// 가방을 묻은 순간 그 안에 있던 개수 (기획서 6.8 「가방 X → 100% 소실」의 실제
    /// 소실량). `Count`는 묻는 순간 `DrainServer`로 이미 0이 돼 있어, 귀환 정산이 그때
    /// 가서 물어보면 늘 0만 나온다 — 정산 시점이 아니라 잃은 시점의 값을 따로 들고
    /// 있어야 한다. 되찾거나 다음 밤이 되면 0으로 되돌아간다.
    public int BuriedLossCount { get; private set; }

    /// 적재가 80%를 넘었는가 (기획서 6.6). 남의 것도 읽을 수 있는 유일한 적재 정보다.
    public bool Overloaded => overloaded.Value;

    /// 겉보기(`LoadVisuals`)가 다시 그리는 신호. 소유자·관전자 양쪽에서 오른다.
    public event System.Action LoadChanged;

    /// 임대료 페널티 3단계는 밴드를 정확히 한 칸 불리하게 옮긴다 (기획서 3.3 밤 항목).
    /// 무게→속도 표 자체는 BB.Rules의 `LoadBands`에 있어 씬 없이 기획서 6.7과 대조할 수 있다.
    ///
    /// **서버에서만 옳은 값이다.** 원장(`TeamLedger`)은 복제되지 않아 클라이언트에서는
    /// 밴드가 안 옮겨진 값이 나온다. 그래서 밖으로 열지 않는다 — 화면에 쓸 값은 복제되는
    /// `PlayerMove.SpeedScale`이다.
    float CurrentSpeedMultiplier
    {
        get
        {
            var ledger = director != null && team != null ? director.LedgerOf(team.Team) : null;
            return LoadBands.SpeedMultiplierShifted(LoadRatio, ledger != null && ledger.WeightBandShifted);
        }
    }

    public bool AddServer(Ingredient item) => AddServer(item, 1);

    /// 칸 하나를 통째로 받는다. 상자 칸은 같은 종류가 쌓여 있으므로 한 번에 여럿이 온다.
    ///
    /// 받았는지 여부를 돌려준다. 조용히 무시하면 안 된다 — 상자는 칸을 비운 뒤에 이걸
    /// 부르므로, 가방을 묻은 채로 칸을 누르면 재료가 어디에도 없이 사라진다.
    public bool AddServer(Ingredient item, int count)
    {
        if (!IsServer || !hasBag.Value || item == Ingredient.None || count <= 0) return false;

        for (var i = 0; i < count; i++)
        {
            items.Add((int)item);
            carried.Value += Ingredients.WeightOf(item);
        }
        return true;
    }

    /// 밤이 끝날 때 복귀 구역 밖에 있으면 적재의 일부를 잃는다 (기획서 6.8).
    /// 완전 소실이라 아무도 주울 수 없다.
    public void LoseShareServer(float share)
    {
        if (!IsServer) return;

        var remaining = new List<Ingredient>();
        foreach (var item in items) remaining.Add((Ingredient)item);
        RandomLoss.TakeShare(remaining, share,
            new System.Random(Random.Range(int.MinValue, int.MaxValue)));

        items.Clear();
        carried.Value = 0f;
        foreach (var item in remaining)
        {
            items.Add((int)item);
            carried.Value += Ingredients.WeightOf(item);
        }
    }

    public void ClearServer()
    {
        if (!IsServer) return;
        items.Clear();
        carried.Value = 0f;
    }

    /// 적재 80% 이상인 상대를 대시로 밀면 일부가 바닥에 쏟아진다 (기획서 6.6).
    /// 쏟아진 것은 누구나 열 수 있는 임시 박스가 된다 (기획서 6.5.4).
    public void DropShareServer(float share, Vector3 at)
    {
        if (!IsServer) return;

        SpawnPilesServer(TakeOutServer(carried.Value * Mathf.Clamp01(share)), at);
    }

    /// 쏟아진 것을 임시 상자로 만든다. 상자 하나는 *종류* 5개까지라서 종류가 넘치면
    /// 여러 개로 쪼개진다 (12종류 → 5/5/2).
    ///
    /// 평면 탑다운이라 중력이 없어(PlayerMove) 스폰 높이가 곧 최종 높이다. 플레이어 위치는
    /// 캡슐 *중심*이므로 그대로 쓰면 더미가 가슴 높이에 뜬 채 영영 내려오지 않는다.
    /// 발밑 높이와 더미 반높이를 둘 다 실제 콜라이더에서 읽는다 — 손으로 맞춘 상수는
    /// 캡슐이나 프리팹 크기를 바꾸는 순간 조용히 어긋난다.
    void SpawnPilesServer(List<Ingredient> dropped, Vector3 at)
    {
        if (dropped == null || dropped.Count == 0) return;
        if (pilePrefab == null)
        {
            Debug.LogError($"{name}: pilePrefab이 비어 있다. 쏟아진 재료가 사라진다.", this);
            return;
        }

        var boxes = LootSlots.Pack(dropped);
        for (var i = 0; i < boxes.Count; i++)
        {
            // 위치를 Instantiate 인자로 넘겨야 한다. 생성 후 transform으로 옮기면
            // Physics.autoSyncTransforms가 false라 아래 bounds가 낡은 값을 준다.
            var spot = at + Vector3.right * (i * pileSpacing);
            var pile = Instantiate(pilePrefab, spot, Quaternion.identity);

            var body = pile.GetComponent<Collider>();
            if (body == null)
                Debug.LogError($"{pilePrefab.name}에 Collider가 없다. 더미를 바닥에 맞출 수 없다.", pilePrefab);
            else
                pile.transform.position += Vector3.up * (FeetY(at) - body.bounds.min.y);

            pile.NetworkObject.Spawn();
            pile.SeedServer(boxes[i]);
        }
    }

    /// 발이 실제로 닿는 높이. 캡슐 바닥에서 `skinWidth`를 더 뺀다 — CharacterController는
    /// 늘 그만큼 떠서 서고(`PlayerMove.OnNetworkSpawn`이 접지 높이에 더해 주는 값과 같은
    /// 것이다), 그 여유분을 빼지 않으면 바닥에 놓는 물건이 전부 8cm 떠 보인다.
    float FeetY(Vector3 at) =>
        at.y - (controller.height * 0.5f - controller.center.y) - controller.skinWidth;

    /// 가방 전체를 넘기고 비운다. 밤의 수확은 복귀 구역에서 팀 재고가 되므로
    /// (기획서 2장), 돌려받은 목록의 소유권은 호출자에게 있다.
    public List<Ingredient> DrainServer()
    {
        var taken = new List<Ingredient>();
        if (!IsServer) return taken;

        foreach (var i in items) taken.Add((Ingredient)i);
        items.Clear();
        carried.Value = 0f;
        return taken;
    }

    /// 잘못 담았거나 버릴 때: 가방에 있던 것이 전부 그 자리에 임시 상자로 쏟아진다.
    /// 가방 자체는 그대로 메고 있다 — 버리는 것은 내용물이지 가방이 아니다.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void DumpRpc()
    {
        var phase = director != null ? director.Phase : null;
        if (phase == null || phase.Current != Phase.Night || items.Count == 0) return;

        SpawnPilesServer(DrainServer(), transform.position);
    }

    /// 서 있는 자리에 가방을 묻는다 (기획서: 무게를 비워 대시를 쓰기 위한 선택).
    /// 묻은 가방은 아군에게만 표시되고, 적이 찾아내면 소각당한다 (`BuriedBag`).
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void BuryRpc()
    {
        var phase = director != null ? director.Phase : null;
        if (phase == null || phase.Current != Phase.Night || !hasBag.Value) return;

        if (buriedBagPrefab == null)
        {
            Debug.LogError($"{name}: buriedBagPrefab이 비어 있다. 가방을 묻을 수 없다.", this);
            return;
        }

        var at = transform.position;
        var bag = Instantiate(buriedBagPrefab, at, Quaternion.identity);

        var body = bag.GetComponent<Collider>();
        if (body == null)
            // 콜라이더가 없으면 `PlayerInteractor`의 트리거 후보에 잡히지 않아 아무도 회수도
            // 소각도 할 수 없고, 접지 보정도 걸리지 않아 가슴 높이에 뜬 채로 남는다.
            Debug.LogError($"{buriedBagPrefab.name}에 Collider가 없다. 가방을 찾을 수도 "
                         + "바닥에 맞출 수도 없다.", buriedBagPrefab);
        else
            bag.transform.position += Vector3.up * (FeetY(at) - body.bounds.min.y);

        // 팀을 먼저 심고 스폰한다. 스폰 뒤에 쓰면 그 값은 다음 틱의 델타로 가고, 적
        // 클라이언트는 팀 미상(-1) 상태로 스폰을 받아 그동안 가방을 그대로 렌더한다.
        // 숨기는 것이 유일한 목적인 오브젝트라 한 틱 노출이면 기능이 없는 것과 같다.
        var buried = DrainServer();
        BuriedLossCount = buried.Count;    // 되찾지 못하면 이 개수가 곧 귀환 정산의 손실량이다

        bag.SeedServer(team != null ? team.Team : -1, buried);
        bag.NetworkObject.SpawnWithObservers = false;   // 보여주는 시점은 BuriedBag이 정한다
        bag.NetworkObject.Spawn();
        hasBag.Value = false;

        // 가방이 없어졌으니 부풀어 있을 이유도 없다. `DrainServer`는 무게만 비우고
        // 겉보기를 갱신하지 않으므로 여기서 한 번 더 민다.
        PushSpeedServer();
    }

    /// 묻어 둔 가방을 도로 멘다. 내용물은 보존된다.
    public void RetrieveServer(List<Ingredient> contents)
    {
        if (!IsServer) return;

        hasBag.Value = true;
        BuriedLossCount = 0;    // 되찾았으니 잃은 게 아니다
        if (contents == null) { PushSpeedServer(); return; }
        for (var i = 0; i < contents.Count; i++) AddServer(contents[i]);
    }

    /// 최소 `weight`만큼 빠질 때까지 아이템을 덜어내고 그 목록을 돌려준다.
    /// 가벼운 것부터 뺀다. 무거운 것부터 빼면 목표치를 최대 160%까지 초과했고 항상 가장
    /// 값비싼 아이템을 가져갔는데, 기획서가 요구하는 동작이 아니다.
    List<Ingredient> TakeOutServer(float weight)
    {
        var taken = new List<Ingredient>();
        if (!IsServer || weight <= 0f) return taken;

        var removed = 0f;
        while (removed < weight && items.Count > 0)
        {
            var lightest = 0;
            for (var i = 1; i < items.Count; i++)
                if (Ingredients.WeightOf((Ingredient)items[i]) <
                    Ingredients.WeightOf((Ingredient)items[lightest])) lightest = i;

            var item = (Ingredient)items[lightest];
            items.RemoveAt(lightest);
            removed += Ingredients.WeightOf(item);
            taken.Add(item);
        }

        carried.Value = Mathf.Max(0f, carried.Value - removed);
        return taken;
    }
}
