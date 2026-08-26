using Unity.Netcode;
using UnityEngine;

/// 그릇 재고 전체를 오브젝트 네 개가 아니라 컴포넌트 하나로 다룬다 (기획서 5.3).
/// 규칙에 필요한 것은 개수뿐이다. 그릇은 깨끗하거나, 사용 중이거나, 더럽다.
public class Dish : NetworkBehaviour
{
    // ponytail: 4는 기획서 기본값이다. 실제 개수는 기획서 14장 #5에서 미결정이다.
    [SerializeField] int total = 4;

    int baseTotal;

    readonly NetworkVariable<int> clean = new();
    readonly NetworkVariable<int> dirty = new();


    public int Clean => clean.Value;
    public int Dirty => dirty.Value;
    public int InUse => total - clean.Value - dirty.Value;

    public override void OnNetworkSpawn()
    {
        baseTotal = total;
        if (IsServer) clean.Value = total;
    }

    /// 주문이 시작될 때 Station이 호출한다. false면 깨끗한 그릇이 없어 새 주문을 받을 수 없다.
    public bool ClaimServer()
    {
        if (!IsServer || clean.Value <= 0) return false;
        clean.Value--;
        return true;
    }

    /// 서빙했든 버렸든 그릇은 더러운 상태로 돌아온다.
    public void SoilServer()
    {
        if (!IsServer || InUse <= 0) return;   // 실제로 나가 있는 그릇만 돌아올 수 있다
        dirty.Value++;
    }

    /// 임대료 페널티 3단계는 그릇 하나를 깨뜨린다. 그날 하루만이다 (기획서 3.3: 모든
    /// 페널티는 하루 유지되고 임대료를 내면 풀린다). 정산 때마다 현재 단계로 호출되므로
    /// 깨지는 만큼 쉽게 복구된다.
    public void SetBreakageServer(bool broken)
    {
        if (!IsServer) return;

        var want = Mathf.Max(1, baseTotal - (broken ? 1 : 0));
        if (want == total) return;

        var delta = want - total;
        total = want;

        if (delta < 0)
        {
            if (clean.Value > 0) clean.Value--;
            else if (dirty.Value > 0) dirty.Value--;
        }
        else clean.Value++;
    }

    public void WashServer()
    {
        if (!IsServer || dirty.Value <= 0) return;
        dirty.Value--;
        clean.Value++;
    }

}
