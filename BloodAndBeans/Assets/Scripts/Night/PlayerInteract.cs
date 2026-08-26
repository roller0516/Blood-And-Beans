using Unity.Netcode;
using UnityEngine;

/// 박스 앞에서 F 홀드: 처음 홀드는 개봉이고, 계속 누르면 하나씩 담는다 (기획서 6.5.1).
/// 개봉은 짧고, 시간이 드는 쪽은 담기다.
///
/// 이 클래스는 더 이상 홀드가 끝났는지 판단하지 않는다. "이 박스를 누르기 시작했다"와
/// "뗐다"만 보고하고 나머지 시간은 서버가 잰다. 소유자가 자기 홀드를 재던 탓에 밤 파밍
/// 루프가 공짜였다 (아키텍처_v1.0.md §1.1). 또한 어떤 박스를 붙잡고 있는지 아는 유일한
/// 지점이라, 대시 중단 처리가 여기를 필요로 한다.
public class PlayerInteract : NetworkBehaviour
{
    ItemBox held;           // 소유자 측: 서버에 알린 대상
    ItemBox serverHeld;     // 서버 측 진실. 대시 중단 처리에 쓴다

    /// 소유자 측: 담으려고 고른 칸 (기획서 6.5.1). 권위 있는 값은 서버가 박스에 들고 있고
    /// 이쪽은 커서를 그리고 다음 칸을 계산하기 위한 것이다.
    int selected;

    public int SelectedSlot => selected;

    public void BeginBoxClient(ItemBox box)
    {
        if (!IsOwner || box == null || held == box) return;
        EndBoxClient();
        held = box;
        selected = 0;       // 박스마다 처음부터 고른다. 서버도 값이 없으면 첫 칸으로 본다
        if (held != null) HoldBeginRpc(held.NetworkObject);
    }

    public void EndBoxClient()
    {
        if (!IsOwner || held == null) return;
        HoldEndRpc();
        held = null;
    }

    /// 담을 칸을 옮긴다 (기획서 6.5.1). 담을 수 있는 칸 사이에서만 돌고 끝에서 되돌아온다.
    /// 못 담는 칸에 커서가 서면 홀드가 고장 난 것처럼 보이기 때문이다.
    ///
    /// 출발점은 지금 커서가 아니라 *실제로 담기는 칸*이다. 고른 칸이 남의 손에 사라진
    /// 뒤에도 커서만 그대로면 한 번 눌러도 화면이 움직이지 않는다.
    public void MoveSelectionClient(int delta)
    {
        if (!IsOwner || held == null || delta == 0) return;

        var count = held.SlotCount;
        if (count <= 0) return;

        var from = held.EffectiveSlot(selected);
        if (from < 0) return;                   // 지금 담을 수 있는 칸이 하나도 없다

        for (var step = 1; step <= count; step++)
        {
            var index = ((from + delta * step) % count + count) % count;
            if (!held.IsTakable(index)) continue;

            selected = index;
            SelectSlotRpc(index);
            return;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer) ReleaseServer();
    }

    /// 발신자가 소유자인지 검사한다. NetworkBehaviour의 `[Rpc(SendTo.Server)]`는 어떤
    /// 클라이언트든 호출할 수 있고, 바로 이 검사가 빠져서 한 팀이 다른 팀의 완성 게이지를
    /// 멈출 수 있었다 (아키텍처_v1.0.md §1.2).
    [Rpc(SendTo.Server)]
    void HoldBeginRpc(NetworkObjectReference box, RpcParams p = default)
    {
        if (p.Receive.SenderClientId != OwnerClientId) return;
        if (!box.TryGet(out var no)) return;

        var target = no.GetComponent<ItemBox>();
        if (target == null) return;

        ReleaseServer();
        serverHeld = target;
        target.BeginHoldServer(OwnerClientId);
    }

    /// 고른 칸을 서버에 알린다. `HoldBeginRpc`와 같은 이유로 발신자가 소유자인지 검사한다 —
    /// 없으면 아무 클라이언트나 남의 담기 대상을 바꿀 수 있다.
    [Rpc(SendTo.Server)]
    void SelectSlotRpc(int index, RpcParams p = default)
    {
        if (p.Receive.SenderClientId != OwnerClientId) return;
        if (serverHeld == null) return;

        serverHeld.SelectSlotServer(OwnerClientId, index);
    }

    [Rpc(SendTo.Server)]
    void HoldEndRpc(RpcParams p = default)
    {
        if (p.Receive.SenderClientId != OwnerClientId) return;
        ReleaseServer();
    }

    void ReleaseServer()
    {
        if (serverHeld != null) serverHeld.CancelHoldServer(OwnerClientId);
        serverHeld = null;
    }

    /// 대시를 맞으면 진행 중인 개봉이 끊기지만 진행도는 절반이 남는다 (기획서 6.6).
    /// 진행도는 이제 박스가 들고 있으므로 중단 처리도 박스로 간다.
    public void InterruptServer()
    {
        if (!IsServer || serverHeld == null) return;
        serverHeld.HalveHoldServer(OwnerClientId);
    }

}
