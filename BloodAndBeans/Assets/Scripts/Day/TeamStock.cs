using Unity.Netcode;
using UnityEngine;

/// 한 팀의 재고 — 밤에 들고 온 것이 곧 낮에 팔 수 있는 것이다 (기획서 2장).
///
/// 코어 루프를 닫은 연결 고리다. 이것이 생기기 전에는 IngredientShelf가 모든 재료를
/// 무한히 내줬고, 팀이 밤에 무엇을 캐 왔든 아무 의미가 없었다.
///
/// 원두와 빵 베이스는 들어오지 않는다. 상비 재료라 선반이 무한으로 준다 (기획서 7.1).
public class TeamStock : NetworkBehaviour
{
    /// (int)Ingredient로 인덱싱한다. 상비 재료 칸은 0으로 남고 읽히지 않는다.
    readonly NetworkList<int> counts = new();

    static readonly int Slots = System.Enum.GetValues(typeof(Ingredient)).Length;

    /// 재고가 바뀌었다. 재료 칸이 자기 위에 늘어놓은 것을 다시 그리는 신호다
    /// (`IngredientShelf`). 서버·클라이언트 양쪽에서 오른다.
    public event System.Action CountsChanged;

    public override void OnNetworkSpawn()
    {
        counts.OnListChanged += OnCountsChanged;

        if (!IsServer) return;

        counts.Clear();                       // 다시 스폰됐을 때 표가 두 벌 쌓이면 안 된다
        for (var i = 0; i < Slots; i++) counts.Add(0);
    }

    public override void OnNetworkDespawn() => counts.OnListChanged -= OnCountsChanged;

    void OnCountsChanged(NetworkListEvent<int> _) => CountsChanged?.Invoke();

    public int CountOf(Ingredient i)
    {
        var slot = (int)i;
        return slot < 0 || slot >= counts.Count ? 0 : counts[slot];
    }

    public void DepositServer(Ingredient i)
    {
        if (!IsServer) return;

        var slot = (int)i;
        if (slot < 0 || slot >= counts.Count) return;
        counts[slot] += 1;
    }

    /// 재고가 없으면 false다. 그 메뉴는 오늘 만들 수 없다는 뜻이다.
    public bool TakeServer(Ingredient i)
    {
        if (!IsServer) return false;

        var slot = (int)i;
        if (slot < 0 || slot >= counts.Count || counts[slot] <= 0) return false;
        counts[slot] -= 1;
        return true;
    }

    /// 팀이 현재 보유한 전부. 주문 예보의 30% 몫에 쓴다 (기획서 5.5 규칙 3).
    /// 할당이 발생한다. 매 프레임이 아니라 전환마다 한 번 호출된다.
    public void CopyHeldTo(System.Collections.Generic.List<Ingredient> outp)
    {
        for (var slot = 0; slot < counts.Count; slot++)
        {
            if (counts[slot] <= 0) continue;
            var i = (Ingredient)slot;
            if (!outp.Contains(i)) outp.Add(i);
        }
    }
}
