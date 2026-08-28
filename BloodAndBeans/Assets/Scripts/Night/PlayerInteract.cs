using Unity.Netcode;
using UnityEngine;

/// 박스 앞에서 F 홀드: 게이지를 채우면 루팅 창이 열리고, 그 뒤로는 창에서 칸을 눌러 담는다
/// (기획서 6.5.1).
///
/// 이 클래스는 홀드가 끝났는지 판단하지 않는다. "이 박스를 누르기 시작했다", "뗐다",
/// "이 칸을 눌렀다"만 보고하고 나머지는 서버가 잰다. 소유자가 자기 홀드를 재던 탓에 밤
/// 파밍 루프가 공짜였다 (아키텍처_v1.0.md §1.1). 또한 어떤 박스를 붙잡고 있는지 아는
/// 유일한 지점이라, 대시 중단 처리가 여기를 필요로 한다.
public class PlayerInteract : NetworkBehaviour
{
    /// 소유자 측: 서버에 알린 대상. F를 놓아도 루팅 세션이 살아 있으므로 여기서 놓지 않는다.
    /// 창을 여닫는 `MatchFlow`가 이 참조와 `ItemBox.Opened`만 보고 판단한다.
    ItemBox held;

    ItemBox serverHeld;     // 서버 측 진실. 대시 중단과 칸 담기가 여기로 간다

    /// 캐스팅을 시작한 시각. 게이지 표시는 표시 전용이라 로컬에서 잰다 — 권위 있는
    /// 진행도는 서버의 `HoldTimer`에 있다.
    float castStart;
    bool casting;

    /// 지금 루팅 창을 띄워야 할 박스. 세션이 닫히면 `ItemBox.Opened`가 false가 된다.
    public ItemBox LootBox => held;

    /// 개봉 게이지 진행도(0~1). 표시 전용이다.
    public float CastProgress01
    {
        get
        {
            if (!casting || held == null || held.Opened) return 0f;
            var required = Mathf.Max(held.RequiredSecondsFor(PlayerTeam.Local()), 0.01f);
            return Mathf.Clamp01((Time.time - castStart) / required);
        }
    }

    public void BeginBoxClient(ItemBox box)
    {
        if (!IsOwner || box == null) return;

        if (held != box)
        {
            if (held != null) CloseBoxRpc();
            held = box;
        }

        castStart = Time.time;
        casting = true;
        HoldBeginRpc(held.NetworkObject);
    }

    /// F를 놓았다. 캐스팅만 끝난다 — 이미 열린 창은 이동하거나 맞을 때까지 유지된다.
    public void EndBoxClient()
    {
        if (!IsOwner) return;
        casting = false;
        if (held != null) HoldEndRpc();
    }

    /// 루팅 창에서 칸을 눌렀다. 담을 수 있는지는 서버가 다시 판단한다.
    public void TakeSlotClient(int index)
    {
        if (!IsOwner || held == null || !held.Opened) return;
        TakeSlotRpc(index);
    }

    /// 창을 스스로 닫는다(다른 박스로 옮기거나 UI를 닫을 때). 세션은 서버가 지운다.
    public void CloseBoxClient()
    {
        if (!IsOwner || held == null) return;
        CloseBoxRpc();
        held = null;
        casting = false;
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

        if (serverHeld != target) ReleaseServer();
        serverHeld = target;
        target.BeginHoldServer(OwnerClientId);
    }

    [Rpc(SendTo.Server)]
    void HoldEndRpc(RpcParams p = default)
    {
        if (p.Receive.SenderClientId != OwnerClientId) return;
        if (serverHeld != null) serverHeld.EndHoldServer(OwnerClientId);
    }

    /// 담을 칸을 서버에 알린다. `HoldBeginRpc`와 같은 이유로 발신자가 소유자인지 검사한다 —
    /// 없으면 아무 클라이언트나 남의 상자를 대신 털 수 있다.
    [Rpc(SendTo.Server)]
    void TakeSlotRpc(int index, RpcParams p = default)
    {
        if (p.Receive.SenderClientId != OwnerClientId) return;
        if (serverHeld != null) serverHeld.TakeStackServer(OwnerClientId, index);
    }

    [Rpc(SendTo.Server)]
    void CloseBoxRpc(RpcParams p = default)
    {
        if (p.Receive.SenderClientId != OwnerClientId) return;
        ReleaseServer();
    }

    void ReleaseServer()
    {
        if (serverHeld != null) serverHeld.CancelSessionServer(OwnerClientId);
        serverHeld = null;
    }

    /// 대시를 맞으면 파밍이 취소된다 (기획서: 이동하거나 피격당하면 상자 UI가 즉시 닫힘).
    /// 진행도를 절반 남기지 않는다 — 다시 열려면 캐스팅부터 처음이다.
    public void InterruptServer()
    {
        if (!IsServer || serverHeld == null) return;
        serverHeld.CancelSessionServer(OwnerClientId);
    }
}
