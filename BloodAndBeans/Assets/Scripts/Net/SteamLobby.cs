using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Steamworks;
using Steamworks.Data;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// 스팀 로비 하나를 방으로 쓰는 매치메이킹. 방 목록·방 만들기·대기실·시작을 담당하고,
/// 팀 정원 판정과 좌석 배정은 서버 권위인 `MatchSeating`이 한다.
///
/// 런처 씬과 함께 살아남는다 (`SteamFacepunchTransport`가 아니라 이쪽이 스팀 세션을 든다).
/// 방 목록은 접속 *전에* 떠야 하므로 스팀 초기화가 트랜스포트보다 먼저 필요하고,
/// 매치를 끝내고 타이틀로 돌아올 때 세션이 살아 있어야 목록을 다시 받을 수 있다.
///
/// 대기실은 NGO가 아니라 스팀 로비 멤버 데이터로 돈다. 아직 아무도 접속하지 않았으므로
/// 팀별 인원을 물어볼 서버가 없다. 호스트가 시작을 누르는 순간부터 서버 권위로 넘어가고,
/// 여기서 고른 팀은 접속 승인 페이로드로 실려 서버의 검사를 받는다.
public class SteamLobby : MonoBehaviour
{
    [Header("스팀")]
    /// 480은 Valve의 Spacewar 테스트 앱이다. 실제 앱 ID를 받으면 여기만 바꾼다.
    [SerializeField] uint steamAppId = 480;

    [Header("팀")]
    /// 이 판의 팀 수. 카페는 정확히 이 수만큼 스폰된다.
    [SerializeField, Min(1)] int teams = 4;

    /// 기획서 10장이 지원하는 최대 팀 수. 치트 툴은 이 위로 올릴 수 없다.
    [SerializeField, Min(1)] int maxTeams = 4;

    /// 한 팀에 앉힐 수 있는 인원. 방 정원(팀 수 × 이 값)의 출처이기도 하다.
    [SerializeField, Min(1)] int playersPerTeam = 2;

    /// 정원이 차서 접속을 거절할 때 클라이언트에게 보내는 사유.
    [SerializeField] string roomFullMessage = "고른 팀도 다른 팀도 자리가 없다.";

    [Header("연결")]
    /// 스팀 트랜스포트. `NetworkManager` 프리팹에 함께 있어 Inspector로는 이을 수 없고,
    /// 매니저가 선 뒤에 한 번만 푼다 (`ResolveTransports`). 같은 프리팹에 두는 구성이라면
    /// 여기 이어 두면 되고, 그때는 찾지 않는다.
    [SerializeField] SteamFacepunchTransport steamTransport;

    /// 스팀을 쓰지 않는 로컬 테스트(MPPM 가상 플레이어, 같은 PC 2인)용 트랜스포트.
    /// 매치가 끝나면 여기로 되돌려 개발 HUD의 Host/Client 버튼이 계속 동작하게 한다.
    [SerializeField] NetworkTransport localTransport;

    [Header("씬")]
    /// 호스트가 시작을 누르면 NGO가 모두에게 이 씬을 로드시킨다. Build Settings에 있어야 한다.
    [SerializeField] string gameScene = "SampleScene";

    /// 매치가 끝나면 돌아올 곳.
    [SerializeField] string titleScene = "Title";

    /// 전투 씬 이름. 이 판의 게임 씬이 무엇인지 아는 유일한 자리다 —
    /// `NetworkAutoStart`가 "지금 그 씬에서 재생했는가"를 판단할 때 되읽는다.
    public string GameScene => gameScene;

    [Header("방 목록")]
    [SerializeField, Min(1)] int roomListLimit = 32;

    /// 방 이름 기본값. {0}에 스팀 이름이 들어간다.
    [SerializeField] string roomNameFormat = "{0}의 방";

    /// 로비 메타데이터 키. 목록 질의 필터로도 쓰이므로 값이 바뀌면 구버전 방이 안 보인다.
    const string GameKey = "bb_game";
    const string GameValue = "blood-and-beans";
    const string NameKey = "bb_room";
    const string HostKey = "bb_host";
    const string LiveKey = "bb_live";

    /// 멤버별 메타데이터 키. 대기실의 팀 선택이 여기로 오간다.
    const string TeamKey = "bb_team";

    readonly List<RoomInfo> rooms = new();
    readonly List<RoomMember> members = new();
    int[] occupancy = new int[0];

    Lobby? current;
    bool ownsSteamSession;
    bool subscribedToNetwork;
    bool subscribedToSteam;
    bool alive = true;

    /// 방 목록의 한 줄.
    public readonly struct RoomInfo
    {
        public RoomInfo(Lobby lobby, string name, ulong hostSteamId, int members, int capacity)
        {
            Lobby = lobby;
            Name = name;
            HostSteamId = hostSteamId;
            Members = members;
            Capacity = capacity;
        }

        public Lobby Lobby { get; }
        public string Name { get; }
        public ulong HostSteamId { get; }
        public int Members { get; }
        public int Capacity { get; }
    }

    /// 대기실의 한 사람.
    public readonly struct RoomMember
    {
        public RoomMember(ulong steamId, string name, int team, bool isSelf, bool isHost)
        {
            SteamId = steamId;
            Name = name;
            Team = team;
            IsSelf = isSelf;
            IsHost = isHost;
        }

        public ulong SteamId { get; }
        public string Name { get; }

        /// 아직 고르지 않았으면 `TeamSeats.NoPreference`.
        public int Team { get; }
        public bool IsSelf { get; }
        public bool IsHost { get; }
    }

    /// 스팀이 살아 있는가. false면 방 목록도 방 만들기도 되지 않는다.
    public bool Ready => SteamClient.IsValid;

    /// 마지막 상태 또는 실패 사유. 화면에 그대로 띄운다.
    public string Status { get; private set; } = string.Empty;

    public IReadOnlyList<RoomInfo> Rooms => rooms;
    public IReadOnlyList<RoomMember> Members => members;

    public bool InRoom => current.HasValue;
    public bool IsRoomHost => current.HasValue && Ready && current.Value.IsOwnedBy(SteamClient.SteamId);
    public string RoomName => current.HasValue ? current.Value.GetData(NameKey) : string.Empty;

    /// 내가 고른 팀. 대기실에 들어갈 때 멤버 데이터로 기록된다.
    public int SelectedTeam { get; private set; }

    /// 이 사람이 고른 캐릭터 (기획서 9장). `CharacterCatalog.All`의 인덱스이고
    /// `NoPick`이면 아직 고르지 않았다.
    ///
    /// 로비에 있는 동안에는 플레이어 오브젝트가 없을 수 있어서 서버에 보낼 자리가 없다.
    /// 그래서 여기 보관했다가 `PlayerCharacter`가 스폰되는 순간 서버로 넘긴다 — 씬을
    /// 건너는 사용자 선택이라 수명이 앱과 같은 이 컴포넌트가 드는 것이 맞다.
    public int SelectedCharacter { get; private set; } = CharacterCatalog.NoPick;

    public void SelectCharacter(int index)
    {
        if (!CharacterCatalog.IsValid(index)) return;

        SelectedCharacter = index;
        Changed?.Invoke();

        // 이미 스폰돼 있으면 지금 보낸다. 아직이면 스폰 때 이 값을 읽어 간다.
        PlayerCharacter.Local()?.PickRpc(index);
    }

    /// 방 목록·대기실·상태 중 무엇이든 바뀌었다. 화면은 매 프레임 새로 그리는 대신 이걸 듣는다.
    public event Action Changed;

    /// 이 판의 좌석 권위. 이 컴포넌트가 소유한다 — 접속 승인은 게임 씬보다 먼저 걸려
    /// 있어야 하고, 씬을 넘어 사는 것은 여기뿐이다.
    public MatchSeating Seating { get; private set; }

    public int TeamCount => Seating != null ? Seating.TeamCount : 1;
    public int PlayersPerTeam => Seating != null ? Seating.PlayersPerTeam : 1;

    /// 한 방에 들어갈 수 있는 인원. 팀 수 × 팀당 인원이며 출처는 `MatchSeating` 하나뿐이다.
    public int RoomCapacity => TeamCount * PlayersPerTeam;

    /// 방 이름 기본값. 입력 필드가 생기면 이 값을 초기값으로 쓴다.
    public string SuggestedRoomName => string.Format(roomNameFormat, Ready ? SteamClient.Name : string.Empty);

    /// 대기실에서 그 팀을 고른 사람 수. 서버가 아니라 스팀 로비가 답하는 값이라 참고용이고,
    /// 최종 판정은 접속 승인이 한다.
    public int OccupancyOf(int team) => team >= 0 && team < occupancy.Length ? occupancy[team] : 0;

    public bool TeamHasRoom(int team) => OccupancyOf(team) < PlayersPerTeam;

    /// 호스트가 시작을 누를 수 있는가. 정원을 넘겨 고른 사람이 있으면 시작해 봐야 그 사람이
    /// 접속 승인에서 튕긴다.
    public bool CanStartMatch => IsRoomHost && members.Count > 0 && !AnyTeamOverfilled();

    void Awake()
    {
        Seating = new MatchSeating(teams, maxTeams, playersPerTeam, roomFullMessage);

        occupancy = new int[Mathf.Max(1, TeamCount)];
    }

    /// 스팀 세션을 연다. `GameManager`의 부팅 사슬이 부르며, Awake에서 스스로 열지 않는다 —
    /// 무엇이 언제 초기화되는지를 호출 순서가 아니라 코드 한 줄로 읽게 하기 위해서다.
    ///
    /// Facepunch의 `SteamClient.Init`은 동기다. 그래도 한 프레임 양보하고 여는 이유는,
    /// 스팀이 꺼져 있을 때 예외가 나기까지 걸리는 시간이 그대로 씬 첫 프레임을 붙잡기
    /// 때문이다. 여기서 기다리는 것은 스팀이 아니라 프레임이다.
    public async UniTask InitializeAsync()
    {
        await UniTask.Yield();
        InitializeSteam();
    }

    // NetworkManager는 이 오브젝트보다 늦게 깨어날 수 있다. Awake 순서에 기대지 않는다.
    void OnEnable()
    {
        SubscribeToNetwork();
        SubscribeToSteam();
    }

    void Start() => SubscribeToNetwork();

    void OnDisable()
    {
        UnsubscribeFromNetwork();
        UnsubscribeFromSteam();
    }

    /// 스팀 콜백 펌프. `SteamClient.Init(appId, asyncCallbacks: false)`로 열었으므로 이 호출이
    /// 없으면 로비 생성·목록·입장 콜백이 영원히 오지 않는다. 매 프레임이어야 하는 몇 안 되는
    /// 처리다.
    void Update()
    {
        if (SteamClient.IsValid) SteamClient.RunCallbacks();
    }

    void OnDestroy()
    {
        alive = false;
        current?.Leave();
        current = null;

        if (ownsSteamSession && SteamClient.IsValid) SteamClient.Shutdown();
    }

    void InitializeSteam()
    {
        if (SteamClient.IsValid) return;

        try
        {
            SteamClient.Init(steamAppId, false);
            ownsSteamSession = true;
            Status = string.Empty;
        }
        catch (Exception e)
        {
            // 스팀이 꺼져 있거나 로그인되지 않은 상태다. 게임을 죽일 이유는 없고, 방 흐름만
            // 잠근 채 로컬 테스트 경로를 남겨 둔다.
            Status = $"스팀에 연결하지 못했다: {e.Message}";
            CDebug.LogWarning($"{name}: {Status}", this);
        }
    }

    // --- 구독 ---

    void SubscribeToNetwork()
    {
        var manager = NetworkManager.Singleton;
        if (subscribedToNetwork || manager == null) return;

        ResolveTransports(manager);

        manager.OnServerStarted += LoadGameSceneServer;
        manager.OnServerStopped += OnNetworkStopped;
        manager.OnClientStopped += OnNetworkStopped;

        // 접속 승인은 StartHost보다 먼저 걸려 있어야 한다. 좌석표가 씬 오브젝트였을 때는
        // 스스로 구독했지만, 이제 소유자인 이쪽이 같은 시점에 함께 건다.
        Seating?.Subscribe(manager);

        subscribedToNetwork = true;
    }

    /// 트랜스포트는 `NetworkManager` 프리팹 쪽에 있다. 프리팹이 갈려 있어 직렬화로 이을 수
    /// 없으므로 매니저가 선 뒤 한 번만 찾는다 — 매니저가 뜨는 것은 프레임 수와 무관한
    /// 사건 한 번이다 (AGENTS.md 참조와 결합도).
    ///
    /// 로컬 트랜스포트는 매니저가 기본으로 들고 있는 것을 그대로 쓴다. "스팀을 쓰지 않을 때
    /// 쓰는 것"이 곧 그 기본값이라, 이름으로 다시 찾을 이유가 없다.
    void ResolveTransports(NetworkManager manager)
    {
        if (localTransport == null) localTransport = manager.NetworkConfig.NetworkTransport;
        if (steamTransport == null) steamTransport = manager.GetComponentInChildren<SteamFacepunchTransport>(true);

        if (steamTransport == null)
            CDebug.LogError($"{name}: {nameof(SteamFacepunchTransport)}를 찾지 못했다. "
                          + "방에 접속할 수 없다.", this);
    }

    void UnsubscribeFromNetwork()
    {
        var manager = NetworkManager.Singleton;
        if (manager == null || !subscribedToNetwork) return;

        manager.OnServerStarted -= LoadGameSceneServer;
        manager.OnServerStopped -= OnNetworkStopped;
        manager.OnClientStopped -= OnNetworkStopped;
        Seating?.Unsubscribe();
        subscribedToNetwork = false;
    }

    /// 스팀 쪽 이벤트는 static이다. 해제하지 않으면 죽은 컴포넌트가 계속 불린다.
    void SubscribeToSteam()
    {
        if (subscribedToSteam) return;

        SteamMatchmaking.OnLobbyMemberJoined += OnMembershipChanged;
        SteamMatchmaking.OnLobbyMemberLeave += OnMembershipChanged;
        SteamMatchmaking.OnLobbyMemberDisconnected += OnMembershipChanged;
        SteamMatchmaking.OnLobbyMemberDataChanged += OnMemberDataChanged;
        SteamMatchmaking.OnLobbyDataChanged += OnLobbyDataChanged;
        SteamMatchmaking.OnLobbyGameCreated += OnHostStartedMatch;
        subscribedToSteam = true;
    }

    void UnsubscribeFromSteam()
    {
        if (!subscribedToSteam) return;

        SteamMatchmaking.OnLobbyMemberJoined -= OnMembershipChanged;
        SteamMatchmaking.OnLobbyMemberLeave -= OnMembershipChanged;
        SteamMatchmaking.OnLobbyMemberDisconnected -= OnMembershipChanged;
        SteamMatchmaking.OnLobbyMemberDataChanged -= OnMemberDataChanged;
        SteamMatchmaking.OnLobbyDataChanged -= OnLobbyDataChanged;
        SteamMatchmaking.OnLobbyGameCreated -= OnHostStartedMatch;
        subscribedToSteam = false;
    }

    void OnMembershipChanged(Lobby lobby, Friend _) => RefreshMembersIfCurrent(lobby);
    void OnMemberDataChanged(Lobby lobby, Friend _) => RefreshMembersIfCurrent(lobby);
    void OnLobbyDataChanged(Lobby lobby) => RefreshMembersIfCurrent(lobby);

    void RefreshMembersIfCurrent(Lobby lobby)
    {
        if (!current.HasValue || lobby.Id != current.Value.Id) return;

        RefreshMembers();
        Changed?.Invoke();
    }

    // --- 방 목록 ---

    public async Task RefreshRoomsAsync()
    {
        if (!Guard()) return;

        Status = "방 목록을 받는 중…";
        Changed?.Invoke();

        Lobby[] found;
        try
        {
            found = await SteamMatchmaking.LobbyList
                                          .WithMaxResults(roomListLimit)
                                          .WithKeyValue(GameKey, GameValue)
                                          .RequestAsync();
        }
        catch (Exception e)
        {
            Fail($"방 목록을 받지 못했다: {e.Message}");
            return;
        }
        if (!alive) return;

        rooms.Clear();
        // 결과가 하나도 없으면 빈 배열이 아니라 null이 온다 (Facepunch LobbyQuery.RequestAsync).
        if (found != null)
            foreach (var lobby in found)
            {
                // 이미 시작한 방은 들어가 봐야 승인 전에 막힌다. 목록에서 뺀다.
                if (!string.IsNullOrEmpty(lobby.GetData(LiveKey))) continue;
                rooms.Add(Describe(lobby));
            }

        Status = rooms.Count > 0 ? string.Empty : "방이 없다.";
        Changed?.Invoke();
    }

    RoomInfo Describe(Lobby lobby)
    {
        var name = lobby.GetData(NameKey);
        ulong.TryParse(lobby.GetData(HostKey), out var host);
        var capacity = lobby.MaxMembers > 0 ? lobby.MaxMembers : RoomCapacity;
        return new RoomInfo(lobby, string.IsNullOrEmpty(name) ? lobby.Id.ToString() : name,
                            host, lobby.MemberCount, capacity);
    }

    // --- 방 만들기 / 참가 ---

    /// 방을 만들고 대기실로 들어간다. 네트워크는 아직 뜨지 않는다 — 팀을 고르고 인원을
    /// 확인하는 동안은 스팀 로비만으로 충분하고, 접속을 먼저 열면 대기실을 나가는 것과
    /// 매치를 나가는 것이 같은 일이 돼 버린다.
    public async Task<bool> CreateRoomAsync(string roomName)
    {
        if (!Guard()) return false;

        Status = "방을 만드는 중…";
        Changed?.Invoke();

        Lobby? created;
        try
        {
            created = await SteamMatchmaking.CreateLobbyAsync(RoomCapacity);
        }
        catch (Exception e)
        {
            Fail($"방을 만들지 못했다: {e.Message}");
            return false;
        }
        if (!alive) return false;

        if (!created.HasValue)
        {
            Fail("방을 만들지 못했다.");
            return false;
        }

        var lobby = created.Value;
        lobby.MaxMembers = RoomCapacity;
        lobby.SetData(GameKey, GameValue);
        lobby.SetData(NameKey, roomName);
        lobby.SetData(HostKey, SteamClient.SteamId.Value.ToString());
        lobby.SetPublic();
        lobby.SetJoinable(true);

        EnterRoom(lobby);
        return true;
    }

    /// 방에 들어가 대기실을 연다. 접속은 호스트가 시작을 누를 때다.
    public async Task<bool> JoinRoomAsync(RoomInfo room)
    {
        if (!Guard()) return false;

        if (room.HostSteamId == 0)
        {
            Fail("방의 호스트를 알 수 없다.");
            return false;
        }

        Status = "방에 들어가는 중…";
        Changed?.Invoke();

        RoomEnter entered;
        try
        {
            entered = await room.Lobby.Join();
        }
        catch (Exception e)
        {
            Fail($"방에 들어가지 못했다: {e.Message}");
            return false;
        }
        if (!alive) return false;

        if (entered != RoomEnter.Success)
        {
            Fail($"방에 들어가지 못했다: {entered}");
            return false;
        }

        EnterRoom(room.Lobby);
        return true;
    }

    void EnterRoom(Lobby lobby)
    {
        current = lobby;
        Status = string.Empty;

        // 내 팀을 바로 적어 둬야 남들 화면의 인원 표시에 내가 잡힌다.
        SelectTeam(FirstTeamWithRoom());
    }

    public void LeaveRoom()
    {
        current?.Leave();
        current = null;
        members.Clear();
        ClearOccupancy();

        var manager = NetworkManager.Singleton;
        if (manager != null && (manager.IsListening || manager.IsClient)) manager.Shutdown();

        Changed?.Invoke();
    }

    // --- 대기실 ---

    /// 팀을 고른다. 스팀 로비 멤버 데이터로 적히므로 같은 방의 모두가 즉시 본다.
    public void SelectTeam(int team)
    {
        if (team < 0 || team >= TeamCount) return;

        SelectedTeam = team;
        if (current.HasValue) current.Value.SetMemberData(TeamKey, team.ToString());

        // 내 변경은 콜백을 기다리지 않고 바로 반영한다. 스팀은 자기 변경을 되돌려 주지
        // 않을 수도 있고, 그러면 내 선택만 화면에서 한 박자 늦는다.
        RefreshMembers();
        Changed?.Invoke();
    }

    void RefreshMembers()
    {
        members.Clear();
        ClearOccupancy();
        if (!current.HasValue) return;

        var lobby = current.Value;
        var ownerId = lobby.Owner.Id;
        var selfId = Ready ? SteamClient.SteamId.Value : 0ul;

        foreach (var member in lobby.Members)
        {
            var isSelf = member.Id == selfId;
            var team = isSelf ? SelectedTeam : ParseTeam(lobby.GetMemberData(member, TeamKey));
            
            if (team >= 0 && team < occupancy.Length) occupancy[team]++;

            members.Add(new RoomMember(member.Id, member.Name, team,
                                       isSelf, member.Id == ownerId));
        }
    }

    static int ParseTeam(string raw) =>
        int.TryParse(raw, out var team) ? team : TeamSeats.NoPreference;

    void ClearOccupancy()
    {
        if (occupancy.Length != Mathf.Max(1, TeamCount)) occupancy = new int[Mathf.Max(1, TeamCount)];
        for (var i = 0; i < occupancy.Length; i++) occupancy[i] = 0;
    }

    int FirstTeamWithRoom()
    {
        for (var team = 0; team < TeamCount; team++)
            if (TeamHasRoom(team)) return team;
        return 0;
    }

    bool AnyTeamOverfilled()
    {
        for (var team = 0; team < occupancy.Length; team++)
            if (occupancy[team] > PlayersPerTeam) return true;
        return false;
    }

    // --- 시작 ---

    /// 호스트 전용. 방을 잠그고 호스트로 뜬 다음 게임 씬을 모두에게 로드시킨다.
    /// 손님은 `SetGameServer`가 일으키는 `OnLobbyGameCreated`를 받고 붙는다 — 스팀이 이
    /// 용도로 준 경로다.
    public bool StartMatch()
    {
        if (!Guard() || !current.HasValue) return false;
        if (!IsRoomHost)
        {
            Fail("방장만 시작할 수 있다.");
            return false;
        }

        var lobby = current.Value;
        lobby.SetJoinable(false);
        lobby.SetData(LiveKey, GameValue);

        if (!StartNetwork(SelectedTeam, host: true, targetSteamId: 0)) return false;

        // 손님에게 "여기로 붙어라"를 알린다. 호스트 자신도 이 콜백을 받으므로 걸러 낸다.
        // 게임 씬은 `LoadGameSceneServer`가 서버 기동 이벤트에서 이미 걸었다.
        lobby.SetGameServer(SteamClient.SteamId);
        return true;
    }

    /// 서버가 뜨면 게임 씬으로 넘어간다. 방에서 시작했든 개발 HUD의 Host 버튼을 눌렀든
    /// 같은 곳으로 가야 한다 — 여기 말고 방 흐름 안에만 두면 스팀 없이 여는 로컬 테스트가
    /// 타이틀에 갇힌다.
    void LoadGameSceneServer()
    {
        var manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer) return;
        if (SceneManager.GetActiveScene().name == gameScene) return;

        StartCoroutine(LoadGameSceneServerCoroutine());
    }

    System.Collections.IEnumerator LoadGameSceneServerCoroutine()
    {
        // StartHost()가 완전히 끝난 뒤 다음 프레임에 씬을 로드해야 NGO 씬 동기화가 꼬이지 않는다.
        yield return null;

        var manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer) yield break;

        var status = manager.SceneManager.LoadScene(gameScene, LoadSceneMode.Single);
        if (status != SceneEventProgressStatus.Started) Fail($"게임 씬을 불러오지 못했다: {status}");
    }

    void OnHostStartedMatch(Lobby lobby, uint ip, ushort port, SteamId server)
    {
        if (!current.HasValue || lobby.Id != current.Value.Id) return;

#if UNITY_EDITOR
        // ParrelSync 등으로 에디터 창을 2개 띄우면 스팀 계정(SteamId)이 똑같습니다.
        // 따라서 SteamId가 아니라 '내가 지금 서버(호스트)로 켜졌는가'로 호스트 여부를 판별해야 합니다.
        var isAlreadyHost = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
        if (!Ready || isAlreadyHost) return;
#else
        if (!Ready || server.Value == SteamClient.SteamId.Value) return;   // 호스트 자신
#endif

        // 붙을 곳은 방장이어야 한다. 스팀도 방 주인이 아닌 멤버의 SetLobbyGameServer를
        // 막지만, 손님이 어디로 접속할지를 이벤트 값 하나만 믿고 정하지는 않는다.
        var owner = current.Value.Owner.Id;
        if (server.Value != owner)
        {
            Fail("방장이 아닌 곳에서 시작 신호가 왔다. 접속하지 않는다.");
            return;
        }

        StartNetwork(SelectedTeam, host: false, server.Value);
    }

    bool StartNetwork(int team, bool host, ulong targetSteamId)
    {
        var manager = NetworkManager.Singleton;
        if (manager == null)
        {
            Fail("NetworkManager가 씬에 없다.");
            return false;
        }

        // 원하는 팀은 접속 승인 페이로드로 간다. 서버가 정원을 보고 받아들이거나 거절한다.
        manager.NetworkConfig.ConnectionData = MatchSeating.EncodeTeamRequest(team);
        
#if UNITY_EDITOR
        manager.NetworkConfig.NetworkTransport = localTransport != null ? localTransport : steamTransport;
#else
        manager.NetworkConfig.NetworkTransport = steamTransport;
#endif
        
        steamTransport.TargetSteamId = targetSteamId;

        var started = host ? manager.StartHost() : manager.StartClient();
        if (!started) Fail(host ? "호스트를 시작하지 못했다." : "접속을 시작하지 못했다.");
        return started;
    }

    /// 매치가 끝나면 스팀 방에서도 나가고, 좌석을 비우고, 타이틀로 돌아간다.
    /// 방에 남아 있으면 남들 목록에 죽은 방이 계속 뜬다.
    void OnNetworkStopped(bool _)
    {
        current?.Leave();
        current = null;
        members.Clear();
        ClearOccupancy();

        var manager = NetworkManager.Singleton;
        if (manager != null && localTransport != null)
            manager.NetworkConfig.NetworkTransport = localTransport;

        // 좌석표는 런처와 함께 살아남으므로 씬을 다시 불러도 저절로 비지 않는다.
        Seating?.ResetForNewMatch();

        Changed?.Invoke();

        if (SceneManager.GetActiveScene().name != titleScene) SceneManager.LoadScene(titleScene);
    }

    void Fail(string reason)
    {
        Status = reason;
        Changed?.Invoke();
    }

    bool Guard()
    {
        if (Ready) return true;

        if (string.IsNullOrEmpty(Status)) Status = "스팀이 준비되지 않았다.";
        Changed?.Invoke();
        return false;
    }
}
