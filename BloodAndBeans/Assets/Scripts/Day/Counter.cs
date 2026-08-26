using Unity.Netcode;
using UnityEngine;

/// 서빙대 (기획서 5.4). 제조 쪽에서만 닿는다. 조리대 아래쪽에 있는 사람은 서빙할 수 없다.
public class Counter : NetworkBehaviour, IInteractable
{
    [SerializeField] float reach = 2.5f;
    Cafe ownerCafe;

    /// 조립 루트는 전역이 아니라 소속 카페에서 받는다. 설비마다 따로 찾으면 카페별로
    /// 다른 답이 나올 여지가 생긴다 (아키텍처_v1.0.md §1.4).
    MatchDirector Director =>
        (ownerCafe != null ? ownerCafe : (ownerCafe = Cafe.Of(this)))?.Director;
    public string Prompt => "서빙대 · 서빙하기";

    public void BeginInteractionClient() => ServeRpc();
    public void EndInteractionClient() { }


    /// 서빙대 안쪽, 즉 -forward가 가리키는 방향.
    bool OnServingSide(Vector3 p)
    {
        var d = p - transform.position;
        return d.magnitude <= reach && Vector3.Dot(d, transform.forward) < 0f;
    }

    [Rpc(SendTo.Server)]
    public void ServeRpc(RpcParams p = default)
    {
        var clientId = p.Receive.SenderClientId;
        if (Director == null || Director.Phase.Current != Phase.Day) return;
        var t = Station.PlayerOf(clientId);
        if (t == null || !OnServingSide(t.position)) return;
        if (!Cafe.SameTeamServer(this, clientId)) return;     // 내 서빙대가 아니다

        var carry = PlayerCarry.Of(clientId);
        if (carry == null || !carry.Held.IsProduct) return;

        var queue = Cafe.Of(this)?.Queue;
        if (queue != null && queue.TryServeServer(carry.Held))
            carry.ClearServer();          // 받는 사람이 없으면 계속 들고 있는다
    }

}
