using Unity.Netcode;
using UnityEngine;

/// 조리대 (기획서 5.4). 제조존과 보급·세척존을 가르는 통과 불가 섬이고, **그 너머로
/// 아이템을 건넬 수 있다** (5.4-2).
///
/// 서빙 카운터는 위쪽에만 붙어 있어서 아래에 있는 사람은 서빙할 수 없다 (5.4-3).
/// 그래서 아래에서 꺼낸 재료를 위로 넘기는 이 통로가 곧 분업의 조건이다. 좌우 끝으로
/// 돌아가는 길만 있으면 둘이 나눠 설 이유가 사라진다.
///
/// **한 칸짜리다.** 올려 둔 것을 누가 집어 가기 전에는 다음 것을 올릴 수 없다. 큐를 두면
/// 조리대가 창고가 되어 동선을 길게 만든 레이아웃 설계가 무의미해진다.
///
/// 기획서 5.1의 디저트 조립(빵 베이스를 조리대에 올리고 크림을 얹은 뒤 오븐에 넣는다)은
/// 여기 없다. 현재 구현은 오븐이 재료를 직접 받는다 (`Station.Insert`). 차이를 보고만
/// 하고 임의로 한쪽을 바꾸지 않는다 (AGENTS.md).
public class PrepIsland : NetworkBehaviour, IInteractable
{
    [SerializeField] float reach = 2.5f;

    /// 서버 권위 내용물. 규칙에 필요한 것은 전부 여기 있고, 화면에 필요한 것만 복제된다.
    HeldItem placed = HeldItem.Nothing;

    /// 표시용. 조리대는 양쪽에서 보이므로 무엇이 올라와 있는지가 전원에게 복제돼야 한다 —
    /// 안 보이면 건네주기가 성립하지 않는다.
    readonly NetworkVariable<CarryView> view = new(CarryView.Nothing);

    Cafe ownerCafe;

    /// 조립 루트는 전역이 아니라 소속 카페에서 받는다. 설비마다 따로 찾으면 카페별로
    /// 다른 답이 나올 여지가 생긴다 (아키텍처_v1.0.md §1.4).
    MatchDirector Director =>
        (ownerCafe != null ? ownerCafe : (ownerCafe = Cafe.Of(this)))?.Director;

    public string Prompt =>
        view.Value.Empty ? "조리대 · 올려두기" : $"조리대 · {view.Value.Label} 집기";

    public void BeginInteractionClient() => UseRpc();
    public void EndInteractionClient() { }

    /// 손에 든 것이 있으면 올려놓고, 비어 있으면 올려진 것을 집는다. 키 하나가 무엇을
    /// 뜻하는지는 서버가 아는 상태(누구 손에 무엇이 있는가)로 갈린다 — `Station.UseRpc`와
    /// 같은 방식이다.
    [Rpc(SendTo.Server)]
    public void UseRpc(RpcParams p = default)
    {
        var clientId = p.Receive.SenderClientId;
        if (Director == null || Director.Phase.Current != Phase.Day) return;
        if (!InReach(clientId)) return;

        // 손이 닿는다고 권한이 있는 것은 아니다. 상대 카페로 걸어 들어가 조리대의 완성품을
        // 집어 갈 수 있으면 안 된다 (`Cafe.SameTeamServer` 주석).
        if (!Cafe.SameTeamServer(this, clientId)) return;

        var carry = PlayerCarry.Of(clientId);
        if (carry == null) return;

        if (!carry.Empty && placed.Empty) Place(carry);
        else if (carry.Empty && !placed.Empty) Take(carry);
    }

    void Place(PlayerCarry carry)
    {
        placed = carry.Held;
        carry.ClearServer();
        view.Value = CarryView.Of(placed);
    }

    void Take(PlayerCarry carry)
    {
        carry.SetServer(placed);
        placed = HeldItem.Nothing;
        view.Value = CarryView.Nothing;
    }

    bool InReach(ulong clientId)
    {
        var t = Station.PlayerOf(clientId);
        return t != null && Vector3.Distance(t.position, transform.position) <= reach;
    }
}
