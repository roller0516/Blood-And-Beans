using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// 무엇을 들고 있고 그 대가가 무엇인지 (기획서 6.7).
/// 칸 제한도 무게 제한도 없다. 얼마든지 더 담을 수 있고, 대신 기어간다.
///
/// 아이템을 총합 숫자가 아니라 개별로 추적한다. 기획서 6.6이 적재분의 일부를 주울 수 있는
/// 더미로 바닥에 흘리게 하는데, 기록하지 않은 것은 흘릴 수 없기 때문이다.
[RequireComponent(typeof(CharacterController))]
public class PlayerInventory : NetworkBehaviour
{
    [SerializeField] float capacity = 20f;
    [SerializeField] ItemBox pilePrefab;

    readonly NetworkList<int> items = new(null,
        NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<float> carried = new(0f,
        NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

    MatchDirector director;

    // 흘린 더미를 발밑에 맞출 때 캡슐 치수가 필요하다. 스폰 시점에 한 번만 찾는다.
    CharacterController controller;

    // CurrentSpeedMultiplier는 PlayerMove의 매 프레임 이동 경로에서 불린다. 주기 실행
    // 안에서 컴포넌트를 조회하지 않는다.
    PlayerTeam team;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        team = GetComponent<PlayerTeam>();
    }

    // 스폰 시점에는 매치 씬이 아직 없다. 직접 캐시하면 null로 굳어 임대료 페널티의
    // 무게 밴드 이동(기획서 3.3)이 적용되지 않는다.
    public override void OnNetworkSpawn() => MatchDirector.Bind(BindDirector);

    public override void OnNetworkDespawn() => MatchDirector.Unbind(BindDirector);

    void BindDirector(MatchDirector next) => director = next;

    public float Carried => carried.Value;
    public float LoadRatio => carried.Value / capacity;
    public int Count => items.Count;

    /// 임대료 페널티 3단계는 밴드를 정확히 한 칸 불리하게 옮긴다 (기획서 3.3 밤 항목).
    /// 무게→속도 표 자체는 BB.Rules의 `LoadBands`에 있어 씬 없이 기획서 6.7과 대조할 수 있다.
    public float CurrentSpeedMultiplier
    {
        get
        {
            var ledger = director != null && team != null ? director.LedgerOf(team.Team) : null;
            return LoadBands.SpeedMultiplierShifted(LoadRatio, ledger != null && ledger.WeightBandShifted);
        }
    }

    public void AddServer(Ingredient item)
    {
        if (!IsServer) return;
        items.Add((int)item);
        carried.Value += Ingredients.WeightOf(item);
    }

    /// 밤이 끝날 때 복귀 구역에 없으면 적재의 절반을 잃는다 (기획서 6.8).
    /// 완전 소실이라 아무도 주울 수 없다.
    public void LoseHalfServer()
    {
        if (!IsServer) return;

        var remaining = new List<Ingredient>();
        foreach (var item in items) remaining.Add((Ingredient)item);
        RandomLoss.TakeHalf(remaining, new System.Random(Random.Range(int.MinValue, int.MaxValue)));

        items.Clear();
        carried.Value = 0f;
        foreach (var item in remaining)
        {
            items.Add((int)item);
            carried.Value += Ingredients.WeightOf(item);
        }
    }

    /// 적재 80% 이상인 상대를 대시로 밀면 일부가 바닥에 쏟아진다 (기획서 6.6).
    /// 쏟아진 것은 누구나 열 수 있는 임시 박스가 된다 (기획서 6.5.4).
    public void DropShareServer(float share, Vector3 at)
    {
        if (!IsServer) return;

        SpawnPileServer(TakeOutServer(carried.Value * Mathf.Clamp01(share)), at);
    }

    /// 더미를 바닥에 놓는다. 평면 탑다운이라 중력이 없어(PlayerMove) 스폰 높이가 곧 최종
    /// 높이다. 플레이어 위치는 캡슐 *중심*이므로 그대로 쓰면 더미가 가슴 높이에 뜬 채
    /// 영영 내려오지 않는다.
    ///
    /// 발밑 높이와 더미 반높이를 둘 다 실제 콜라이더에서 읽는다. 캡슐이나 더미 프리팹
    /// 크기를 바꿔도 따라오게 하려는 것이다. 손으로 맞춘 상수는 조용히 어긋난다.
    void SpawnPileServer(List<Ingredient> dropped, Vector3 at)
    {
        if (dropped.Count == 0 || pilePrefab == null) return;

        // 위치를 Instantiate 인자로 넘겨야 한다. 생성 후 transform으로 옮기면
        // Physics.autoSyncTransforms가 false라 아래 bounds가 낡은 값을 준다.
        var pile = Instantiate(pilePrefab, at, Quaternion.identity);

        var body = pile.GetComponent<Collider>();
        if (body == null)
            Debug.LogError($"{pilePrefab.name}에 Collider가 없다. 더미를 바닥에 맞출 수 없다.", pilePrefab);
        else
        {
            var feet = at.y - (controller.height * 0.5f - controller.center.y);
            pile.transform.position += Vector3.up * (feet - body.bounds.min.y);
        }

        pile.NetworkObject.Spawn();
        pile.SeedServer(dropped);
    }

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

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void DumpRpc()
    {
        var phase = director != null ? director.Phase : null;
        if (phase == null || phase.Current != Phase.Night || items.Count == 0) return;

        SpawnPileServer(DrainServer(), transform.position);
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
