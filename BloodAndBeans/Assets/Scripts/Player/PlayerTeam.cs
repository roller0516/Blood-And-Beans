using System.Collections;
using Unity.Netcode;
using UnityEngine;

/// 이 플레이어가 속한 카페. 한 팀은 1~2인이므로 카페가 둘이면 먼저 들어온 두 클라이언트가
/// 각각 팀 0과 팀 1이 되고, 세 번째와 네 번째가 그 팀에 합류한다.
public class PlayerTeam : NetworkBehaviour
{
    readonly NetworkVariable<int> team = new();

    public int Team => team.Value;

    /// 구독해 둔 페이즈 시계. 플레이어는 타이틀 씬에서 스폰되고 매치 씬은 그 뒤에 서므로
    /// 스폰 시점에는 구독할 대상이 없다. 해제할 때 같은 인스턴스를 잡고 있어야 한다.
    GamePhase subscribedPhase;

    /// 자리 배정은 서버가 스폰 뒤에 하므로 표현 쪽은 스폰이 아니라 값의 변화를 따라가야 한다
    /// (Customer.SpeciesChanged와 같은 이유).
    public event System.Action<int> TeamChanged;

    public override void OnNetworkSpawn()
    {
        // 구독은 서버 가드보다 먼저다. 팀 값을 받아 색을 입히는 것은 클라이언트 쪽 일이다.
        team.OnValueChanged += OnTeamValueChanged;
        TeamChanged?.Invoke(team.Value);

        if (!IsServer) return;

        // 팀 명단은 더 이상 이 컴포넌트의 일이 아니다. 여기서 카페 수를 세던 코드가
        // "팀이 몇 개인가"에 답하던 여섯 곳 중 하나였다 (아키텍처_v1.0.md §1.4).
        //
        // 좌석은 런처와 함께 사는 `MatchSeating`에 묻는다. `MatchDirector`가 아니다 —
        // 플레이어는 타이틀 씬에서 스폰되고 매치 씬은 그 뒤에 로드되므로, 스폰 시점에
        // 매치 씬 오브젝트를 찾으면 없다. 찾을 수 없는 것을 팀 0으로 때우면 정원 초과와
        // 무소속이 전부 팀 0의 카페 열쇠가 된다.
        var seating = MatchSeating.Instance;
        if (seating == null)
        {
            Debug.LogError($"{name}: {nameof(MatchSeating)}가 없다. 런처 씬을 거치지 않았다는 "
                         + "뜻이고, 이 플레이어는 팀을 받을 수 없다.", this);
            return;
        }

        team.Value = seating.SeatServer(OwnerClientId);
        if (team.Value == TeamSeats.NoSeat)
        {
            // 접속 승인이 잡아 준 자리가 없다. 팀 0으로 되돌리면 정원 초과가 남의 카페
            // 접근 권한이 되므로, 무소속(-1)으로 두고 어디에도 손대지 못하게 한다.
            Debug.LogError($"{name}: 앉힐 자리가 없다. 방 정원을 넘겨 들어온 클라이언트다.", this);
            return;
        }

        ApplyMatchSceneStateServer();
    }

    /// 매치 씬에 있는 것들(카페 복제 범위, 박스 상태, 안개 소속)을 이 플레이어에게 맞춘다.
    /// 스폰 시점에는 매치 씬이 아직 없을 수 있어 아무것도 하지 않고 돌아가고, 그때는
    /// `MatchDirector`가 카페를 스폰하면서 다시 부른다.
    public void ApplyMatchSceneStateServer()
    {
        if (!IsServer || team.Value < 0) return;

        var director = MatchDirector.Instance;
        if (director == null) return;

        director.ApplyTeamVisibilityServer(OwnerClientId, team.Value);
        foreach (var box in FindObjectsByType<ItemBox>(FindObjectsSortMode.None))
            box.SendStateToClientServer(OwnerClientId, team.Value);
        //GetComponent<FogOfWar>()?.JoinTeamServer();
        StartCoroutine(ApplyVisibilityAfterSceneSpawn(director));

        // 밤이 시작될 때마다 팀의 숲 진입 지점으로 되돌린다. 첫 판은 페이즈가 이미 밤으로
        // 들어간 뒤에 이 클라이언트가 붙을 수 있으므로 아래에서 현재 페이즈로 한 번 더 배치한다.
        if (subscribedPhase == null)
        {
            subscribedPhase = director.Phase;
            subscribedPhase.PhaseEntered += OnPhaseEntered;
        }

        MoveToPhaseStartServer(director, director.Phase.Current);
    }

    void OnPhaseEntered(Phase p)
    {
        var director = MatchDirector.Instance;
        if (IsServer && p == Phase.Night && director != null) MoveToPhaseStartServer(director, p);
    }

    /// 밤은 숲 가장자리, 그 외에는 자기 팀 카페. 카페는 런타임에 스폰되므로 씬에
    /// 직렬화된 시작 위치를 쓸 수 없다.
    void MoveToPhaseStartServer(MatchDirector director, Phase p)
    {
        if (!IsServer || team.Value < 0) return;

        var slot = TeamSlotServer();
        var destination = p == Phase.Night
            ? director.NightSpawnPosition(team.Value, slot)
            : director.CafeSpawnPosition(team.Value, slot);
        if (destination.HasValue) PlayerTeleport.ToServer(gameObject, destination.Value);
    }

    /// 팀 안에서 이 플레이어의 자리 번호. 좌석표는 팀만 돌려주므로 같은 팀에서 나보다
    /// 먼저 들어온 사람 수로 센다. 배치할 때만 부르는 조회다.
    int TeamSlotServer()
    {
        var slot = 0;
        foreach (var client in NetworkManager.ConnectedClientsList)
            if (client.ClientId < OwnerClientId && Of(client.ClientId) == team.Value) slot++;
        return slot;
    }

    public override void OnNetworkDespawn()
    {
        team.OnValueChanged -= OnTeamValueChanged;
        if (subscribedPhase != null)
        {
            subscribedPhase.PhaseEntered -= OnPhaseEntered;
            subscribedPhase = null;
        }
    }

    void OnTeamValueChanged(int _, int now) => TeamChanged?.Invoke(now);

    IEnumerator ApplyVisibilityAfterSceneSpawn(MatchDirector director)
    {
        yield return null;
        director.ApplyTeamVisibilityServer(OwnerClientId, team.Value);
    }

    /// 해당 클라이언트가 속한 팀. 서버 측 답이다. 예전에 ItemBox에 있던 조회는 대신
    /// *로컬* 클라이언트에게 물었고, 그래서 서버가 엉뚱한 팀의 페널티로 박스 개봉 속도를
    /// 계산했다 (아키텍처_v1.0.md §1.1).
    /// 실패는 팀이 아니다. 여기서 유효한 팀 번호를 돌려주면 컴포넌트 누락이 팀 0 설비
    /// 접근 권한으로 둔갑한다.
    public static int Of(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.ConnectedClients.TryGetValue(clientId, out var c)) return -1;

        var t = c.PlayerObject != null ? c.PlayerObject.GetComponent<PlayerTeam>() : null;
        return t != null ? t.Team : -1;
    }

    /// 이 클라이언트가 속한 팀. 표시와 로컬 입력 차단 용도로만 쓴다.
    ///
    /// HUD와 게이지가 매 프레임 부르는 자리라 `GetComponent`를 캐시한다. 캐시 키는
    /// 로컬 PlayerObject 자체이므로 재접속이나 씬 재시작으로 플레이어가 바뀌면 자동으로
    /// 무효가 된다. static이지만 저장하는 것은 이번 프레임의 조회 결과뿐이고 게임 상태는
    /// 아니다.
    static NetworkObject cachedOwner;
    static PlayerTeam cachedLocal;

    public static int Local()
    {
        var nm = NetworkManager.Singleton;
        var po = nm != null && nm.IsClient && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
        if (po == null)
        {
            cachedOwner = null;
            cachedLocal = null;
            return -1;
        }

        if (!ReferenceEquals(po, cachedOwner))
        {
            cachedOwner = po;
            cachedLocal = po.GetComponent<PlayerTeam>();
        }
        return cachedLocal != null ? cachedLocal.Team : -1;
    }
}
