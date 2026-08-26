using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// F를 누르고 있으면 더러운 그릇을 씻는다 (기획서 5.3). 탄 제품을 버리는 곳이기도 하다.
/// 버리면 매출은 0이고 그릇은 그대로 더러운 더미로 간다.
///
/// 세척 시간은 다른 모든 홀드와 마찬가지로 서버가 잰다. 예전에는 클라이언트가 시간을 세고
/// 세척 완료를 통보했다 (아키텍처_v1.0.md §1.1).
public class Sink : NetworkBehaviour, IInteractable
{
    // ponytail: 임시값. 기획서 14장 #5에서 세척 시간이 미결정이다.
    [SerializeField] float washSeconds = 1.5f;
    [SerializeField] float reach = 2.5f;

    readonly HoldTimer hold = new();
    readonly List<ulong> holders = new();

    Cafe ownerCafe;

    /// 조립 루트는 전역이 아니라 소속 카페에서 받는다. 설비마다 따로 찾으면 카페별로
    /// 다른 답이 나올 여지가 생긴다 (아키텍처_v1.0.md §1.4).
    MatchDirector Director =>
        (ownerCafe != null ? ownerCafe : (ownerCafe = Cafe.Of(this)))?.Director;
    public string Prompt => "싱크대 · 길게 눌러 설거지 / 버리기로 폐기";

    public void BeginInteractionClient() => WashHoldBeginRpc();
    public void EndInteractionClient() => WashHoldEndRpc();


    void Update()
    {
        if (!IsServer) return;

        hold.CopyClientsTo(holders);
        for (var i = 0; i < holders.Count; i++) Tick(holders[i]);
    }

    /// F를 누르고 있는 동안 세척 시간마다 더러운 그릇 하나씩 처리한다.
    void Tick(ulong clientId)
    {
        if (Director == null || Director.Phase.Current != Phase.Day)
        {
            hold.Cancel(clientId);
            return;
        }
        if (!InReach(clientId)) { hold.Cancel(clientId); return; }
        if (!hold.TryConsume(clientId, NetworkManager.ServerTime.Time, washSeconds)) return;
        Cafe.Of(this)?.Dishes?.WashServer();
    }

    [Rpc(SendTo.Server)]
    public void WashHoldBeginRpc(RpcParams p = default)
    {
        var clientId = p.Receive.SenderClientId;
        if (Director == null || Director.Phase.Current != Phase.Day) return;
        if (!InReach(clientId) || !Cafe.SameTeamServer(this, clientId)) return;
        hold.Begin(clientId, NetworkManager.ServerTime.Time);
    }

    [Rpc(SendTo.Server)]
    public void WashHoldEndRpc(RpcParams p = default) => hold.Cancel(p.Receive.SenderClientId);

    [Rpc(SendTo.Server)]
    public void DiscardRpc(RpcParams p = default)
    {
        var clientId = p.Receive.SenderClientId;
        if (Director == null || Director.Phase.Current != Phase.Day) return;
        if (!InReach(clientId) || !Cafe.SameTeamServer(this, clientId)) return;

        var carry = PlayerCarry.Of(clientId);
        if (carry == null || !carry.Held.IsProduct) return;

        carry.ClearServer();
        Cafe.Of(this)?.Dishes?.SoilServer();
    }

    public override void OnNetworkDespawn() => hold.CancelAll();

    bool InReach(ulong clientId)
    {
        var t = Station.PlayerOf(clientId);
        return t != null && Vector3.Distance(t.position, transform.position) <= reach;
    }

}
