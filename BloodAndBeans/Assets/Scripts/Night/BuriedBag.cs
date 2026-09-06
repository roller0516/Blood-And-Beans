using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// 땅에 묻어 둔 가방 (기획서: 무게를 비워 대시를 쓰기 위해 그 자리에 숨긴다).
///
/// 아군에게만 보인다. 적은 눈으로 찾을 수 없고, 가방 없이 돌아다니는 동선을 힌트로
/// 위치를 유추해 그 위에 올라서야 존재를 안다.
///
/// 복제 자체를 끊으면 적이 밟아도 아무 일도 일어나지 않으므로, 아군에게는 스폰 즉시
/// 보여 주고(`OnNetworkSpawn`) 적에게는 실제로 다가온 순간에만 `NetworkShow`한다
/// (`RevealToNearbyEnemiesServer`). 표시(Renderer)를 팀별로 끄는 것과는 별개다 — 렌더러
/// 토글은 이미 복제받은 클라이언트가 그리는지 여부일 뿐이라, 스폰 시점부터 전원에게
/// 복제해 버리면 위치·팀 정보 자체가 상대 클라이언트 메모리에 항상 올라가 있고
/// 렌더러만 꺼진 상태가 된다 — 조작된 클라이언트가 렌더러를 강제로 켜면 찾지 않고도
/// 모든 적 은닉 위치를 알 수 있다. `NetworkShow`를 접근 시점으로 미루면 애초에 그
/// 클라이언트에 오브젝트 자체가 존재하지 않아 읽을 것이 없다.
///
/// **숨김이 기본값이다.** 팀을 아직 모르는 동안 보이는 쪽으로 열어 두면, 숨기는 것이
/// 유일한 목적인 오브젝트가 그 창 동안 전원에게 드러난다. 모를 때는 감춘다.
///
/// F 홀드의 결과는 팀에 따라 갈린다. 아군은 회수하고, 적은 소각한다. 시간 측정과 판정은
/// 전부 서버가 한다.
[RequireComponent(typeof(NetworkObject))]
public class BuriedBag : NetworkBehaviour, IInteractable
{
    [SerializeField] float reach = 2f;

    /// 아군이 도로 메는 데 걸리는 시간. 적이 태우는 쪽보다 짧다 — 자기 물건이다.
    [SerializeField] float retrieveSeconds = 0.6f;

    /// 적이 소각을 완료하는 데 걸리는 시간 (기획서: F키를 꾹 눌러 캐스팅).
    [SerializeField] float burnSeconds = 3f;

    /// 캐스팅이 끊기는 이동 거리. 상자 루팅과 같은 규칙이다 — 움직이면 취소된다.
    /// 넉백으로 밀려나도 같은 거리로 걸리므로 피격 취소가 함께 성립한다.
    [SerializeField] float cancelDistance = 0.35f;

    /// 아군에게만 켜지는 표시물. 위치 아이콘·마커를 여기 자식으로 둔다.
    [SerializeField] GameObject teamMarker;

    /// 소각 연출. 없으면 연출 없이 사라진다.
    [SerializeField] ParticleSystem burnEffect;

    readonly NetworkVariable<int> ownerTeam = new(-1);

    /// 스폰 전에 서버가 심어 두는 값. 스폰 뒤에 쓰면 그 값은 다음 틱의 델타로 가고,
    /// 그동안 적 클라이언트는 팀 미상 상태의 가방을 받는다 (`Cafe.AssignTeamServer`와 같은 이유).
    int pendingTeam = -1;

    readonly List<Ingredient> contents = new();     // 서버 전용
    PlayerInventory sourceInventory;               // 묻은 사람의 소실 기록도 서버에만 둔다

    /// 진행 중인 홀드 하나. 팀과 시작 위치를 여기 캐시한다 — 틱에서 `PlayerTeam.Of`를
    /// 부르면 그 안의 `GetComponent`가 주기 실행 안의 컴포넌트 조회가 된다 (AGENTS.md).
    struct Hold
    {
        public Transform Body;
        public int Team;
        public Vector3 From;
    }

    readonly HoldTimer hold = new();
    readonly Dictionary<ulong, Hold> holds = new();
    readonly List<ulong> scratch = new();

    MatchDirector director;

    /// 표시 갱신에 쓰는 캐시. 주기 실행 안에서 컴포넌트를 조회하지 않는다.
    Renderer[] bodyRenderers;

    /// 표시를 확정했는가. 로컬 팀과 소유 팀이 둘 다 정해져야 답할 수 있다.
    bool visibilityResolved;

    public float Reach => reach;

    public string Prompt => PlayerTeam.Local() == ownerTeam.Value
        ? "가방 회수 (길게)"
        : "가방 소각 (길게)";

    void Awake()
    {
        // 소각 연출의 Renderer는 빼 둔다. 여기 섞이면 적에게 숨기려고 끈 상태 그대로라
        // 정작 불을 붙인 적에게 불이 보이지 않는다.
        var all = GetComponentsInChildren<Renderer>(true);
        var body = new List<Renderer>(all.Length);
        foreach (var r in all)
            if (burnEffect == null || !r.transform.IsChildOf(burnEffect.transform))
                body.Add(r);
        bodyRenderers = body.ToArray();

        SetShown(false);                 // 모르는 동안은 감춘다
    }

    public override void OnNetworkSpawn()
    {
        // 서버 OnNetworkSpawn은 관측자에게 스폰 메시지를 보내기 전에 실행된다. 여기서
        // 넣은 값이 스폰 페이로드에 실린다 (`Cafe.OnNetworkSpawn`과 같은 순서).
        if (IsServer && pendingTeam >= 0) ownerTeam.Value = pendingTeam;

        director = MatchDirector.Instance;
        if (director != null) director.Phase.PhaseEntered += OnPhaseEntered;

        // 아군에게는 스폰 즉시 보여 준다. `SpawnWithObservers = false`로 스폰됐으므로
        // (`PlayerInventory.BuryRpc`/`PlayerCharacter.PlaceDecoyBagServer`) 여기서 보여
        // 주지 않으면 아군도 이 오브젝트를 영영 못 받는다.
        if (IsServer && director != null && ownerTeam.Value >= 0)
            director.ShowToTeamServer(NetworkObject, ownerTeam.Value);

        ownerTeam.OnValueChanged += OnOwnerTeamChanged;
        ApplyVisibility();
    }

    public override void OnNetworkDespawn()
    {
        if (director != null) director.Phase.PhaseEntered -= OnPhaseEntered;
        ownerTeam.OnValueChanged -= OnOwnerTeamChanged;
        CancelAllHolds();
    }

    /// 밤이 끝나면 회수되지 않은 가방은 그대로 사라진다. 안에 있던 것은 전량 소실이다
    /// (기획서: 가방 미소지는 소환 위치 도착 여부와 무관하게 100% 소실).
    void OnPhaseEntered(Phase p)
    {
        CancelAllHolds();
        if (IsServer && p != Phase.Night) DespawnServer();
    }

    void OnOwnerTeamChanged(int _, int __) => ApplyVisibility();

    /// 지난 프레임에 「추적」이 켜져 있었는가. 꺼지는 순간을 잡아 한 번만 다시 그린다.
    bool trackedWasOn;

    /// 아군이면 보여 주고 아니면 감춘다. 둘 중 하나라도 모르면 감춘 채로 두고 다음
    /// 기회에 다시 온다 — 로컬 플레이어는 늦게 스폰될 수 있고, 그때 재시도 경로가
    /// 없으면 적 화면에 가방이 그대로 남는다.
    ///
    /// 「추적」에 걸린 동안에는 적에게도 보인다 (기획서 9.2).
    void ApplyVisibility()
    {
        var owner = ownerTeam.Value;
        var local = PlayerTeam.Local();
        if (owner < 0 || local < 0) return;

        visibilityResolved = true;
        SetShown(local == owner || TrackedNow);
    }

    /// 「추적」이 드러낸 시각. 이 시각까지 적에게도 보인다.
    float trackedUntil;

    bool TrackedNow => Time.time < trackedUntil;

    /// 「추적」이 이 가방을 찾아냈다 (기획서 9.2). 찾은 사람에게만 보여 준다 — 스킬을
    /// 쓴 쪽의 정보이지 판 전체에 공개되는 것이 아니다.
    public void RevealToServer(ulong clientId, float seconds)
    {
        if (!IsServer || seconds <= 0f) return;

        // 「추적」은 원래 안 보이던 클라이언트를 겨냥한다. 먼저 복제해 주지 않으면
        // 그 클라이언트에는 이 오브젝트 자체가 없어서 RPC를 받을 대상이 없다.
        if (!NetworkObject.IsNetworkVisibleTo(clientId)) NetworkObject.NetworkShow(clientId);

        TrackedRpc(seconds, RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    void TrackedRpc(float seconds, RpcParams p = default)
    {
        trackedUntil = Mathf.Max(trackedUntil, Time.time + seconds);
        ApplyVisibility();
    }

    void SetShown(bool value)
    {
        if (bodyRenderers != null)
            foreach (var r in bodyRenderers) if (r != null) r.enabled = value;
        if (teamMarker != null) teamMarker.SetActive(value);
    }

    /// 스폰 *전에* 서버가 부른다. 팀과 내용물을 심어 두면 스폰 페이로드에 함께 나간다.
    public void SeedServer(int team, List<Ingredient> items, PlayerInventory source = null)
    {
        pendingTeam = team;
        sourceInventory = source;
        contents.Clear();
        if (items != null) contents.AddRange(items);
    }

    public void BeginInteractionClient() => BeginHoldRpc();

    public void EndInteractionClient() => EndHoldRpc();

    [Rpc(SendTo.Server)]
    void BeginHoldRpc(RpcParams p = default)
    {
        // 이 오브젝트는 서버 소유라 `RpcInvokePermission.Owner`를 걸 수 없다. 대신 발신자
        // id를 *행위자 본인의 식별자로만* 쓴다 — 아래 검사와 처리가 전부 그 발신자 자신의
        // 위치·팀·인벤토리를 향하므로, 남을 대신하거나 남의 홀드를 건드릴 경로가 없다.
        var clientId = p.Receive.SenderClientId;
        if (director == null || director.Phase.Current != Phase.Night) return;
        if (holds.ContainsKey(clientId)) return;

        var body = Station.PlayerOf(clientId);
        if (body == null || Vector3.Distance(body.position, transform.position) > reach) return;

        var team = PlayerTeam.Of(clientId);
        if (team < 0) return;

        holds[clientId] = new Hold { Body = body, Team = team, From = body.position };
        hold.Begin(clientId, NetworkManager.ServerTime.Time);
    }

    [Rpc(SendTo.Server)]
    void EndHoldRpc(RpcParams p = default) => CancelHold(p.Receive.SenderClientId);

    void Update()
    {
        // 로컬 팀이 늦게 정해지는 경우를 위한 재시도. 확정되면 bool 하나만 보고 빠진다.
        if (!visibilityResolved) ApplyVisibility();

        // 「추적」이 끝나는 프레임에 다시 감춘다. 남겨 두면 한 번 찾힌 가방이 그 밤 내내
        // 보인다 — 쿨타임 30초짜리 스킬이 영구 표시가 되면 안 된다.
        if (trackedWasOn && !TrackedNow)
        {
            trackedWasOn = false;
            ApplyVisibility();
        }
        else if (!trackedWasOn && TrackedNow) trackedWasOn = true;

        if (!IsServer) return;

        RevealToNearbyEnemiesServer();

        hold.CopyClientsTo(scratch);
        for (var i = 0; i < scratch.Count; i++) TickServer(scratch[i]);
    }

    /// 다가온 적에게만 그 순간 복제를 열어 준다 (기획서 6.7 「적 가방 탐색 및 파괴」:
    /// "적도 숨긴 위치 바로 위에 있으면 [제거] 아이콘이 보임"). `reach`를 그대로 쓰는
    /// 이유는 상호작용 판정(`BeginHoldRpc`)과 같은 거리여야 보이는 순간과 누를 수 있는
    /// 순간이 어긋나지 않기 때문이다. 한 번 보여 주면 다시 숨기지 않는다 — 멀어졌다고
    /// 되돌리면 상호작용 도중에 오브젝트가 사라지는 경우가 생긴다.
    ///
    /// `ConnectedClientsList`와 `Station.PlayerOf`는 딕셔너리 조회일 뿐 씬 탐색이 아니라
    /// 매 프레임 불러도 된다 (AGENTS.md 「참조와 결합도」가 막는 것은 `GameObject.Find`·
    /// `GetComponent` 계열이다).
    void RevealToNearbyEnemiesServer()
    {
        var manager = NetworkManager.Singleton;
        if (manager == null || ownerTeam.Value < 0) return;

        foreach (var client in manager.ConnectedClientsList)
        {
            if (NetworkObject.IsNetworkVisibleTo(client.ClientId)) continue;

            var enemyTeam = PlayerTeam.Of(client.ClientId);
            if (enemyTeam < 0 || enemyTeam == ownerTeam.Value) continue;

            var body = Station.PlayerOf(client.ClientId);
            if (body == null || Vector3.Distance(body.position, transform.position) > reach) continue;

            NetworkObject.NetworkShow(client.ClientId);
        }
    }

    void TickServer(ulong clientId)
    {
        if (director == null || director.Phase.Current != Phase.Night ||
            !holds.TryGetValue(clientId, out var h) || h.Body == null)
        {
            CancelHold(clientId);
            return;
        }

        // 움직이면 캐스팅이 끊긴다 (기획서: 이동하거나 피격당하면 취소). 입력이 아니라
        // 실제 이동 거리를 본다 — 대시에 밀려나는 동안에는 입력이 죽어 있어서, 입력만
        // 보면 넉백으로 끌려가면서도 소각이 계속된다.
        if (Vector3.Distance(h.Body.position, h.From) > cancelDistance ||
            Vector3.Distance(h.Body.position, transform.position) > reach)
        {
            CancelHold(clientId);
            return;
        }

        var mine = h.Team == ownerTeam.Value;
        var now = NetworkManager.ServerTime.Time;
        if (!hold.TryConsume(clientId, now, mine ? retrieveSeconds : burnSeconds)) return;

        if (mine) RetrieveServer(clientId);
        else BurnServer();
    }

    void RetrieveServer(ulong clientId)
    {
        var inv = InventoryOf(clientId);

        // 이미 가방을 메고 있으면 회수하지 않는다. 그대로 두면 한 사람이 가방 둘을 메고
        // 무게 규칙이 무너진다.
        if (inv == null || inv.HasBag) { CancelHold(clientId); return; }

        inv.RetrieveServer(contents);
        if (sourceInventory != null) sourceInventory.ResolveBuriedLossServer(contents.Count);
        contents.Clear();
        DespawnServer();
    }

    /// 소각. 내용물은 아무도 줍지 못하고 사라진다.
    void BurnServer()
    {
        contents.Clear();
        BurnEffectRpc();
        DespawnServer();
    }

    /// 연출은 despawn 전에 보낸다. despawn된 NetworkObject로는 RPC가 나가지 않는다.
    ///
    /// 렌더러는 건드리지 않는다. 주인에게는 이미 켜져 있고, 여기서 전원에게 켜면 소각에
    /// 관여하지 않은 제3팀 화면에도 그 자리에 가방이 있었다는 사실이 드러난다.
    [Rpc(SendTo.ClientsAndHost, InvokePermission = RpcInvokePermission.Server)]
    void BurnEffectRpc()
    {
        if (burnEffect == null) return;

        // 연출은 누구에게나 보인다. 본체 Renderer와 달리 팀으로 가리지 않는다 — 불을 붙인
        // 적도, 가방을 잃은 주인도 무슨 일이 있었는지 봐야 한다.
        foreach (var r in burnEffect.GetComponentsInChildren<Renderer>(true)) r.enabled = true;

        // 이 오브젝트는 곧 despawn된다. 연출을 떼어 내지 않으면 불이 붙는 프레임에 같이
        // 사라진다. 떼어 낸 뒤에는 아무도 소유하지 않으므로 재생 길이만큼만 살려 둔다.
        burnEffect.transform.SetParent(null, true);
        burnEffect.Play();
        Destroy(burnEffect.gameObject, burnEffect.main.duration + burnEffect.main.startLifetimeMultiplier);
    }

    void CancelHold(ulong clientId)
    {
        hold.Cancel(clientId);
        holds.Remove(clientId);
    }

    void CancelAllHolds()
    {
        hold.CancelAll();
        holds.Clear();
    }

    void DespawnServer()
    {
        CancelAllHolds();
        if (NetworkObject != null && NetworkObject.IsSpawned) NetworkObject.Despawn();
    }

    static PlayerInventory InventoryOf(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.ConnectedClients.TryGetValue(clientId, out var c)) return null;
        return c.PlayerObject != null ? c.PlayerObject.GetComponent<PlayerInventory>() : null;
    }
}
