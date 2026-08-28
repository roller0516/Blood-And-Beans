using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using UnityEngine.SceneManagement;

/// 한 판의 카페와 팀별 원장을 든다. "팀이 몇이고 누가 어느 팀인가"는 `MatchSeating`이
/// 답하고, 이 클래스는 그 답을 되읽어 쓴다 — 씬이 갈리면서 좌석 판정만 런처로 옮겼다.
/// 팀 번호의 출처는 여전히 하나다.
///
/// 예전에는 여섯 군데가 각자 답했다 — 씬에 직렬화된 teamId 두 개, 클라이언트 id의 나머지
/// 연산, Cafe 오브젝트 수, 계산대 수, 그 수를 다시 읽는 코드 (아키텍처_v1.0.md §1.4).
/// 이 프로젝트의 팀 격리 결함은 전부 그 여섯이 어긋나면서 생겼다.
///
/// 이제 카페는 씬에 배치되지 않는다. 서버가 팀 수만큼 프리팹을 스폰하고 팀 번호를 직접
/// 찍어 준다. 클라이언트는 스폰 페이로드로 같은 값을 받는다.
///
/// 의도적으로 NetworkBehaviour가 아니다. 원장과 좌석 카운터는 서버 전용이고 어디로도
/// 보내지 않는다. 시계는 이 오브젝트의 `GamePhase`가 가진다.
[RequireComponent(typeof(GamePhase))]
/// 씬 한정 싱글턴이다(`MonoSingleton`). 영속(`PersistentMonoSingleton`)으로 두면 안 되는
/// 이유가 둘이다.
/// 1. 이 컴포넌트는 `GamePhase`와 한 GameObject에 있고 그 오브젝트에는 `NetworkObject`가
///    있다. `DontDestroyOnLoad`가 오브젝트를 매치 씬 밖으로 빼면 NGO의 씬 오브젝트 스폰
///    대상에서 빠져 `GamePhase`가 영영 스폰되지 않는다 — 시계도 카페도 시작하지 않는다.
/// 2. 팀별 원장은 판마다 새로 만들어야 한다. 판을 넘어 살아남으면 지난 판의 빚을 끌고 온다.
public class MatchDirector : MonoSingleton<MatchDirector>
{
    [Header("카페")]
    [SerializeField] Cafe cafePrefab;

    /// 카페를 X축으로 늘어놓는 간격. 씬에 손으로 놓던 배치를 대신하는 규칙이라
    /// 맵 크기에 맞춰 조정해야 한다.
    [SerializeField] float cafeSpacing = 40f;
    [SerializeField] float centerGap = 60f;
    [SerializeField] Vector3 cafeOrigin = Vector3.zero;

    [Header("밤")]
    /// 밤 시작 지점이 놓이는 원의 반지름. 숲 가장자리와 같아야 팀이 맵 가장자리에서
    /// 출발해 가운데로 들어온다.
    [SerializeField] float nightSpawnRadius = 30f;

    /// 배치할 때 지면 위로 띄우는 높이. transform 원점이 캡슐 중심이므로(CharacterController
    /// center 0, height 2) 캡슐 반높이와 같아야 발이 지면에 닿는다. 크면 공중에 뜬 채로 남는다.
    /// 평면 탑다운이라 중력이 없어서(PlayerMove) 한번 뜨면 스스로 내려오지 않는다.
    [SerializeField] float spawnHeight = 1f;

    /// 같은 팀 사람끼리 벌리는 간격. 캡슐 지름보다 커야 서로 밀어내지 않는다.
    [SerializeField] float spawnSlotSpacing = 2f;

    [Header("표시")]
    /// 안개 표시용 평면. NetworkObject가 없는 순수 뷰라서 피어마다 각자 하나씩 만든다.
    [SerializeField] GameObject fogPlanePrefab;

    /// 스폰된 카페의 팀별 색인. 서버는 스폰하면서, 클라이언트는 복제를 받으면서 채운다.
    readonly Dictionary<int, Cafe> cafes = new();

    TeamLedger[] ledgers = new TeamLedger[0];
    GamePhase phase;
    int teamCount;
    bool subscribed;

    public GamePhase Phase => phase;
    public int TeamCount => teamCount;

    /// 판 전체의 매출판. 이 오브젝트에 함께 붙어 있고 씬 오브젝트라 모든 클라이언트에
    /// 복제된다 — 매출만 공개라는 기획서 3.1을 만족하는 유일한 자리다.
    public Scoreboard Board { get; private set; }

    /// 이 오브젝트가 설 때까지 기다리는 쪽에 한 번 알린다. 플레이어는 타이틀 씬에서
    /// 스폰되고 매치 씬은 그 뒤에 로드되므로, 스폰 시점에 `Instance`를 캐시하면 영영
    /// null이다 — 시계 구독도 함께 빠져 밤마다 도는 초기화가 전부 죽는다.
    static System.Action<MatchDirector> ready;

    /// 이미 서 있으면 지금, 아니면 설 때 한 번 부른다. `OnNetworkDespawn` 등에서
    /// 반드시 `Unbind`로 짝을 맞춘다. 핸들러는 같은 인스턴스로 두 번 불려도 되게 짠다.
    public static void Bind(System.Action<MatchDirector> onReady)
    {
        ready += onReady;

        // 씬에는 있지만 아직 Awake 전인 인스턴스는 시계도 팀 수도 비어 있다. 그때는
        // 지금 부르지 않고 Awake의 알림에 맡긴다.
        if (Instance != null && Instance.phase != null) onReady(Instance);
    }

    public static void Unbind(System.Action<MatchDirector> onReady) => ready -= onReady;

    /// 싱글턴 등록과 중복 파괴는 기반 클래스가 한다. `base.Awake()`를 빠뜨리면 `Instance`가
    /// 채워지지 않아 조회하는 쪽이 전부 새 GameObject를 만들어 버린다 (CS0114 경고의 내용).
    protected override void Awake()
    {
        base.Awake();

        // 중복이면 기반 클래스가 이 오브젝트를 파괴하기로 했다. 씬 참조를 풀거나 안개
        // 평면을 만들면 사라질 오브젝트가 남긴 쓰레기가 된다.
        if (Instance != this) return;

        phase = GetComponent<GamePhase>();
        Board = GetComponent<Scoreboard>();
        if (Board == null)
            Debug.LogError($"{name}: {nameof(Scoreboard)}가 같은 오브젝트에 없다. "
                         + "매출과 임대료 정산이 전부 0이 된다.", this);

        // 좌석 권위는 런처 씬에 있고 이 씬보다 먼저 존재한다. 씬 로드 때 한 번만 푼다.
        //seating = MatchSeating.Find();
        if (MatchSeating.Instance == null)
        {
            Debug.LogError($"{name}: {nameof(MatchSeating)}가 없다. 런처 씬을 거치지 않고 "
                         + "게임 씬을 직접 실행했다는 뜻이다. 팀 수를 알 수 없다.", this);
            return;
        }

        ApplyTeamCount(MatchSeating.Instance.TeamCount);

        if (fogPlanePrefab != null) Instantiate(fogPlanePrefab);

        // 팀 수와 시계가 다 선 뒤에 알린다. 그 전에 부르면 받은 쪽이 0팀짜리 판을 본다.
        ready?.Invoke(this);
    }

    // OnEnable 시점에는 NetworkManager가 아직 깨어나지 않았을 수 있다. 씬 오브젝트 사이의
    // Awake/OnEnable 순서는 정해져 있지 않아서 Start까지 두 번 구독을 시도한다.
    // 이미 서버가 떠 있는 경로는 sceneLoaded까지 기다린다. 그 전에 만들면 NGO의
    // OnProcessScene이 로드 중인 런타임 카페를 씬 배치 객체로 다시 처리한다.
    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SubscribeToServerStart();
    }

    void Start() => SubscribeToServerStart();

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnsubscribeFromServerStart();
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode _)
    {
        if (scene == gameObject.scene && NetworkManager.Singleton?.IsServer == true)
            SpawnCafesServer();
    }

    void SubscribeToServerStart()
    {
        var manager = NetworkManager.Singleton;
        if (subscribed || manager == null) return;

        manager.OnServerStarted += SpawnCafesServer;
        subscribed = true;
    }

    void UnsubscribeFromServerStart()
    {
        var manager = NetworkManager.Singleton;
        if (manager != null)
        {
            manager.OnServerStarted -= SpawnCafesServer;
        }
        subscribed = false;
    }

    /// 서버가 뜬 직후 한 번. 클라이언트가 붙기 전이므로 관측자 없이 스폰해 두고, 팀원이
    /// 들어올 때 그 팀 카페만 보여 준다 (`ApplyTeamVisibilityServer`).
    void SpawnCafesServer()
    {
        // 이미 스폰돼 있으면 두 번 하지 않는다. 구독과 직접 호출이 겹칠 수 있고,
        // 이 색인은 카페가 despawn되면 스스로 빈다(`UnregisterCafe`).
        if (cafes.Count > 0) return;
        if (cafePrefab == null)
        {
            Debug.LogError($"{name}: cafePrefab이 비어 있다. 카페가 하나도 생기지 않는다.", this);
            return;
        }

        for (var team = 0; team < teamCount; team++)
        {
            var cafe = Instantiate(cafePrefab, CafePosition(team), Quaternion.identity);
            cafe.AssignTeamServer(team);
            cafe.BindDirectorServer(this);   // 카페 밑 설비들은 전역이 아니라 이 참조를 쓴다

            var networkObject = cafe.GetComponent<NetworkObject>();
            networkObject.SpawnWithObservers = false;   // 상대 팀에는 애초에 복제하지 않는다
            networkObject.Spawn();
        }

        // 이미 붙어 있는 사람들은 타이틀 씬에서 스폰됐다. 그때는 카페도 박스도 없었으므로
        // 복제 범위와 안개 소속이 비어 있다. 매치 씬이 선 지금 채워 준다.
        var manager = NetworkManager.Singleton;
        if (manager == null) return;

        foreach (var client in manager.ConnectedClientsList)
        {
            var player = client.PlayerObject != null ? client.PlayerObject.GetComponent<PlayerTeam>() : null;
            if (player != null) player.ApplyMatchSceneStateServer();
        }
    }

    /// 팀 수가 몇이든 원점을 중심으로 대칭이 되게 늘어놓되, 숲 영역(centerGap)과 겹치지 않게 밀어낸다.
    Vector3 CafePosition(int team)
    {
        float offset = (team - (teamCount - 1) * 0.5f) * cafeSpacing;
        if (team < teamCount / 2f)
            offset -= centerGap * 0.5f;
        else
            offset += centerGap * 0.5f;

        return cafeOrigin + Vector3.right * offset;
    }

    /// 밤의 시작 지점 (기획서: 밤에는 모든 팀이 같은 숲에 선다). 팀 수만큼 원 둘레를
    /// 균등하게 나눠 맵 가장자리에 세우므로, 어느 팀도 다른 팀보다 숲 중앙에 가깝지 않다.
    /// 팀 0이 -X에서 시작해 카페 배치 순서와 방향이 같다.
    /// `slot`은 팀 안에서의 자리 번호다. 같은 점에 두 명을 놓으면 CharacterController끼리
    /// 겹친 채 시작해 서로 밀어낸다.
    public Vector3 NightSpawnPosition(int team, int slot)
    {
        var angle = Mathf.PI + Mathf.PI * 2f * team / Mathf.Max(1, teamCount);
        var radial = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));

        // 원 둘레를 따라 벌린다. 반지름 방향으로 벌리면 뒷사람이 숲 밖으로 밀려난다.
        var tangent = new Vector3(-radial.z, 0f, radial.x);

        return cafeOrigin
             + radial * nightSpawnRadius
             + tangent * (slot * spawnSlotSpacing)
             + Vector3.up * spawnHeight;
    }

    /// 낮·전환의 시작 지점. 자기 팀 카페 위다. 팀이 없거나 카페가 아직 없으면 null.
    public Vector3? CafeSpawnPosition(int team, int slot)
    {
        var cafe = CafeOf(team);
        if (cafe == null) return null;

        return cafe.transform.position
             + Vector3.right * (slot * spawnSlotSpacing)
             + Vector3.up * spawnHeight;
    }

    /// 카페가 스폰되면 자기를 등록한다. 서버와 클라이언트 양쪽에서 실행된다.
    public void RegisterCafe(Cafe cafe)
    {
        if (cafe == null) return;
        if (cafes.TryGetValue(cafe.TeamId, out var existing) && existing != null && existing != cafe)
        {
            Debug.LogError($"{name}: 팀 {cafe.TeamId} 카페가 둘이다 ({existing.name}, {cafe.name}). "
                         + "팀 번호의 출처가 다시 갈라졌다는 뜻이다.", this);
            return;
        }
        cafes[cafe.TeamId] = cafe;
    }

    public void UnregisterCafe(Cafe cafe)
    {
        if (cafe != null && cafes.TryGetValue(cafe.TeamId, out var found) && found == cafe)
            cafes.Remove(cafe.TeamId);
    }

    public Cafe CafeOf(int team) => cafes.TryGetValue(team, out var cafe) ? cafe : null;

    /// 서버 전용. 명단에 있는 팀이면 절대 null이 아니다.
    public TeamLedger LedgerOf(int team) =>
        team >= 0 && team < ledgers.Length ? ledgers[team] : null;

    /// 팀 수를 바꾸면 원장도 같이 새로 만들어야 한다. 이 컴포넌트는 매치 씬과 함께
    /// 살고 죽으므로 판마다 새 원장을 쓴다. 이걸 대체한 딕셔너리는 static이라 한 판의
    /// 빚을 다음 판까지 끌고 갔다.
    void ApplyTeamCount(int count)
    {
        teamCount = count;
        ledgers = new TeamLedger[teamCount];
        for (var i = 0; i < teamCount; i++) ledgers[i] = new TeamLedger();
        cafes.Clear();
    }

    /// 이 클라이언트에게 자기 팀 카페만 보여 준다. 카페는 관측자 없이 스폰되므로
    /// 여기서 보여 주지 않은 카페는 그 클라이언트에 복제 자체가 되지 않는다.
    public void ApplyTeamVisibilityServer(ulong clientId, int team)
    {
        var manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer) return;

        var cafe = CafeOf(team);
        if (cafe != null && cafe.NetworkObject.IsSpawned &&
            !cafe.NetworkObject.IsNetworkVisibleTo(clientId))
            cafe.NetworkObject.NetworkShow(clientId);

        foreach (var customer in FindObjectsByType<Customer>(FindObjectsSortMode.None))
            if (customer.TeamId == team && customer.NetworkObject.IsSpawned &&
                !customer.NetworkObject.IsNetworkVisibleTo(clientId))
                customer.NetworkObject.NetworkShow(clientId);
    }

    public void ShowToTeamServer(NetworkObject networkObject, int team)
    {
        var manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer || networkObject == null || !networkObject.IsSpawned) return;
        foreach (var client in manager.ConnectedClientsList)
            if (PlayerTeam.Of(client.ClientId) == team && !networkObject.IsNetworkVisibleTo(client.ClientId))
                networkObject.NetworkShow(client.ClientId);
    }
}
