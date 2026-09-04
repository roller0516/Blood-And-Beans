using Unity.Netcode;
using UnityEngine;

/// 서빙대 (기획서 5.4). 제조 쪽에서만 닿는다. 조리대 아래쪽에 있는 사람은 서빙할 수 없다.
///
/// 「자동 서빙대」를 설치하면 대기 칸이 하나 생긴다 (기획서 8장: "완성품을 올려두면
/// 자동으로 손님에게 나간다"). 받을 손님이 아직 없어도 완성품을 내려놓을 수 있고, 손님이
/// 오면 사람 없이 나간다 — 만드는 쪽이 서빙 타이밍에 묶이지 않는 것이 이 업그레이드다.
public class Counter : NetworkBehaviour, IInteractable, IItemHolder
{
    [SerializeField] float reach = 2.5f;

    /// 자동 서빙대가 손님을 다시 확인하는 간격.
    /// ponytail: 기획서 8장에 수치가 없다. 손으로 서빙하는 것보다 눈에 띄게 굼떠야
    /// 자동화가 공짜가 되지 않는다. 표가 생기면 옮긴다.
    [SerializeField] float autoServeInterval = 1f;

    /// 서버 권위. 자동 서빙대에 올려 둔 완성품 하나.
    HeldItem pending = HeldItem.Nothing;

    /// 표시용 사본. 규칙용 원본과 항상 함께 바뀐다 (`Station.SetProductServer`와 같은 이유).
    readonly NetworkVariable<CarryView> pendingView = new(CarryView.Nothing);

    /// 다음으로 손님을 확인할 서버 시각.
    double nextTry;

    Cafe ownerCafe;

    /// 조립 루트는 전역이 아니라 소속 카페에서 받는다. 설비마다 따로 찾으면 카페별로
    /// 다른 답이 나올 여지가 생긴다 (아키텍처_v1.0.md §1.4).
    MatchDirector Director => Owner?.Director;

    Cafe Owner => ownerCafe != null ? ownerCafe : (ownerCafe = Cafe.Of(this));

    bool HasAutoServer => Owner != null && Owner.HasUpgrade(UpgradeId.AutoServer);

    public string Prompt =>
        HasAutoServer && !pendingView.Value.Empty
            ? $"서빙대 · {pendingView.Value.Label} 대기 중"
            : "서빙대 · 서빙하기";

    public void BeginInteractionClient() => ServeRpc();
    public void EndInteractionClient() { }

    // --- 표시 (기획서 8장의 대기 칸) ---

    public event System.Action ContentsChanged;
    public int SlotCount => HasAutoServer ? 1 : 0;

    public CarryView SlotAt(int index) =>
        HasAutoServer && index == 0 ? pendingView.Value : CarryView.Nothing;

    public int HighlightSlot => -1;

    public override void OnNetworkSpawn()
    {
        pendingView.OnValueChanged += OnPendingChanged;

        if (Owner != null) Owner.UpgradesChanged += OnUpgradesChanged;
        if (Director != null && Director.Phase != null)
            Director.Phase.PhaseEntered += OnPhaseEntered;

        ContentsChanged?.Invoke();
    }

    public override void OnNetworkDespawn()
    {
        pendingView.OnValueChanged -= OnPendingChanged;

        if (ownerCafe != null) ownerCafe.UpgradesChanged -= OnUpgradesChanged;
        if (Director != null && Director.Phase != null)
            Director.Phase.PhaseEntered -= OnPhaseEntered;
    }

    void OnPendingChanged(CarryView _, CarryView __) => ContentsChanged?.Invoke();
    void OnUpgradesChanged() => ContentsChanged?.Invoke();

    /// 낮이 끝나면 대기 칸을 비운다. 그대로 두면 그 완성품이 물고 있는 그릇이 다음 날까지
    /// 잠긴 채로 남는다 (기획서 5.3: 그릇은 유한하다).
    void OnPhaseEntered(Phase p)
    {
        if (!IsServer || p == Phase.Day || pending.Empty) return;

        SetPendingServer(HeldItem.Nothing);
        Owner?.Dishes?.SoilServer();
    }

    /// 서빙대 안쪽, 즉 -forward가 가리키는 방향.
    ///
    /// 거리는 표면에서 잰다 — 카운터는 8m짜리 한 덩어리라 원점에서 재면 가운데 2.5m
    /// 밖에서는 서빙이 통째로 무시된다 (`Station.WithinReach`).
    bool OnServingSide(Vector3 p) =>
        Station.WithinReach(surface, transform, p, reach) &&
        Vector3.Dot(p - transform.position, transform.forward) < 0f;

    /// 손이 닿는지 재는 기준면 (`Station.WithinReach`).
    Collider surface;

    void Awake() => surface = GetComponentInChildren<Collider>(true);

    [Rpc(SendTo.Server)]
    public void ServeRpc(RpcParams p = default)
    {
        var clientId = p.Receive.SenderClientId;
        if (Director == null || Director.Phase.Current != Phase.Day) return;
        var t = Station.PlayerOf(clientId);
        if (t == null || !OnServingSide(t.position)) return;
        if (!Cafe.SameTeamServer(this, clientId)) return;     // 내 서빙대가 아니다

        var carry = PlayerCarry.Of(clientId);

        // 빈손이면 대기 칸에 올려 둔 것을 도로 집는다. 잘못 올렸을 때 되돌릴 길이 없으면
        // 그 완성품과 그릇이 낮이 끝날 때까지 묶인다.
        if (carry != null && carry.Empty)
        {
            if (!HasAutoServer || pending.Empty) return;
            carry.SetServer(pending);
            SetPendingServer(HeldItem.Nothing);
            return;
        }

        if (carry == null || !carry.Held.IsProduct) return;

        var queue = Owner?.Queue;
        if (queue != null && queue.TryServeServer(carry.Held))
        {
            carry.ClearServer();          // 받는 사람이 없으면 계속 들고 있는다
            return;
        }

        // 받을 손님이 없다. 자동 서빙대가 있으면 올려 두고 손님을 기다린다 (기획서 8장).
        if (!HasAutoServer || !pending.Empty) return;

        SetPendingServer(carry.Held);
        carry.ClearServer();
        nextTry = NetworkManager.ServerTime.Time + autoServeInterval;
    }

    /// 올려 둔 것이 있으면 손님이 올 때마다 스스로 나간다 (기획서 8장).
    void Update()
    {
        if (!IsServer || pending.Empty) return;
        if (Director == null || Director.Phase.Current != Phase.Day) return;

        var now = NetworkManager.ServerTime.Time;
        if (now < nextTry) return;
        nextTry = now + autoServeInterval;

        var queue = Owner?.Queue;
        if (queue != null && queue.TryServeServer(pending)) SetPendingServer(HeldItem.Nothing);
    }

    /// 규칙용 원본과 표시용 사본은 한 곳에서만 바꾼다. 갈라지면 서빙대에 있지도 않은
    /// 것이 보인다 (`Station.SetProductServer`와 같은 이유).
    void SetPendingServer(HeldItem item)
    {
        pending = item;
        pendingView.Value = CarryView.Of(item);
    }
}
