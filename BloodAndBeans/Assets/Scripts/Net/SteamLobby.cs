using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// 방 하나를 다루는 매치메이킹. 방 목록·방 만들기·대기실·시작을 담당하고, 팀 정원 판정과
/// 좌석 배정은 서버 권위인 `MatchSeating`이 한다.
///
/// 방을 **어디에** 두는지는 백엔드가 정한다 (`ILobbyBackend`). 빌드는 스팀 로비를 쓰고,
/// 에디터는 개발 콘솔에서 고른 대로 스팀이거나 창끼리 붙는 로컬 로비다 — 이 클래스는
/// 어느 쪽이든 같은 규칙으로 돈다.
///
/// 런처 씬과 함께 살아남는다 (`SteamFacepunchTransport`가 아니라 이쪽이 스팀 세션을 든다).
/// 방 목록은 접속 *전에* 떠야 하므로 스팀 초기화가 트랜스포트보다 먼저 필요하고,
/// 매치를 끝내고 타이틀로 돌아올 때 세션이 살아 있어야 목록을 다시 받을 수 있다.
///
/// 대기실은 NGO가 아니라 로비 백엔드로 돈다. 아직 아무도 접속하지 않았으므로 팀별 인원을
/// 물어볼 서버가 없다. 호스트가 시작을 누르는 순간부터 서버 권위로 넘어가고, 여기서 고른
/// 팀은 접속 승인 페이로드로 실려 서버의 검사를 받는다.
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

    /// 로비 UI가 팀 수 선택 범위의 상한을 여기서 읽는다 — 값을 UI에 따로 박으면
    /// `maxTeams`를 고쳐도 UI가 안 따라온다.
    public int MaxTeams => maxTeams;

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
    [SerializeField] string[] mapScenes;
    public bool IsGameScene(string scene) => scene == gameScene || (mapScenes != null && Array.IndexOf(mapScenes, scene) >= 0);

    [Header("방 목록")]
    [SerializeField, Min(1)] int roomListLimit = 32;

    /// 방 이름 기본값. {0}에 로비 표시 이름이 들어간다.
    [SerializeField] string roomNameFormat = "{0}의 방";

    readonly List<LobbyRoom> rooms = new();
    readonly List<RoomMember> members = new();
    int[] occupancy = new int[0];

    /// 방을 어디에 두는가. 무엇으로 돌지는 `NetPlatformSetting`이 정한다.
    ILobbyBackend backend;

    bool subscribedToNetwork;
    bool alive = true;

    /// 지금 도는 플랫폼. 개발 콘솔이 표시하고 `SwitchPlatform`으로 바꾼다.
    public NetPlatform Platform { get; private set; }

    /// 대기실의 한 사람.
    public readonly struct RoomMember
    {
        public RoomMember(ulong steamId, string name, int team, bool isSelf, bool isHost,
                          bool isReady, int character)
        {
            SteamId = steamId;
            Name = name;
            Team = team;
            IsSelf = isSelf;
            IsHost = isHost;
            IsReady = isReady;
            Character = character;
        }

        public ulong SteamId { get; }
        public string Name { get; }

        /// 아직 고르지 않았으면 `TeamSeats.NoPreference`.
        public int Team { get; }
        public bool IsSelf { get; }
        public bool IsHost { get; }

        /// 준비를 눌렀는가 (기획서 10.1 「팀 배정과 준비 상태」). 방장은 항상 준비로 친다 —
        /// 시작 버튼을 쥔 사람에게 준비 버튼을 또 주면 누를 이유가 없는 버튼이 하나 는다.
        public bool IsReady { get; }

        /// 고른 캐릭터. `CharacterCatalog.NoPick`이면 아직 안 골랐다.
        /// 팀원끼리는 서로의 픽이 보여야 중복 픽 금지가 성립한다 (기획서 3.4 · 9.1).
        public int Character { get; }
    }

    /// 로비가 살아 있는가. false면 방 목록도 방 만들기도 되지 않는다.
    public bool Ready => backend != null && backend.Ready;

    /// 마지막 상태 또는 실패 사유. 화면에 그대로 띄운다.
    public string Status { get; private set; } = string.Empty;

    public IReadOnlyList<LobbyRoom> Rooms => rooms;
    public IReadOnlyList<RoomMember> Members => members;

    public bool InRoom => backend != null && backend.InRoom;
    public bool IsRoomHost => backend != null && backend.IsHost;
    public string RoomName => backend != null ? backend.RoomName : string.Empty;

    /// 내가 고른 팀. 대기실에 들어갈 때 멤버 데이터로 기록된다.
    public int SelectedTeam { get; private set; }

    /// 내가 준비를 눌렀는가. 방장은 시작 버튼을 쥐므로 항상 참이다.
    public bool SelfReady => IsRoomHost || selfReady;
    bool selfReady;

    /// 준비한 사람 수. 화면이 「준비 N/M」으로 쓴다 (기획서 10.1).
    ///
    /// **방장은 세지 않는다.** 방장은 준비 대신 시작 버튼을 쥐므로 (`SelfReady`가 항상 참),
    /// 세면 아무도 준비하지 않아도 1/N으로 시작한다.
    public int ReadyCount
    {
        get
        {
            var n = 0;
            for (var i = 0; i < members.Count; i++)
                if (members[i].IsReady && !members[i].IsHost) n++;
            return n;
        }
    }

    /// 준비를 눌러야 하는 사람 수. 방장을 뺀 손님 수다 — <see cref="ReadyCount"/>의 분모다.
    public int ReadyTotal
    {
        get
        {
            var n = 0;
            for (var i = 0; i < members.Count; i++) if (!members[i].IsHost) n++;
            return n;
        }
    }

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

        // 팀·준비와 같은 통로로 내보낸다. 서버 판정(`PlayerCharacter.PickRpc`)이 최종이지만,
        // 그건 스폰 뒤라서 로비 화면이 그때까지 남의 픽을 모른다 (기획서 3.4).
        WriteSelf();

        RefreshMembers();
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
    public string SuggestedRoomName =>
        string.Format(roomNameFormat, backend != null ? backend.SelfName : string.Empty);

    /// 대기실에서 그 팀을 고른 사람 수. 서버가 아니라 로비가 답하는 값이라 참고용이고,
    /// 최종 판정은 접속 승인이 한다.
    public int OccupancyOf(int team) => team >= 0 && team < occupancy.Length ? occupancy[team] : 0;

    public bool TeamHasRoom(int team) => OccupancyOf(team) < PlayersPerTeam;

    /// 호스트가 시작을 누를 수 있는가. 정원을 넘겨 고른 사람이 있으면 시작해 봐야 그 사람이
    /// 접속 승인에서 튕기고, 아직 준비하지 않은 사람이 있으면 기다린다 (기획서 10.1).
    public bool CanStartMatch =>
        IsRoomHost && members.Count > 0 && !AnyTeamOverfilled() && ReadyCount >= ReadyTotal;

    void Awake()
    {
        ReconfigureSeating(teams);
        UseBackend(NetPlatformSetting.Current);
    }

    /// 좌석표를 다시 짠다. 방을 만들 때(고른 팀 수)와 방에 들어갈 때(호스트가 고른 팀 수를
    /// 읽어서) 둘 다 이 한 곳을 거친다 — `Seating`과 `occupancy`가 따로 놀면 인원 표시와
    /// 실제 접속 승인이 서로 다른 팀 수를 기준으로 계산된다.
    void ReconfigureSeating(int teamCount)
    {
        Seating = new MatchSeating(Mathf.Clamp(teamCount, 1, maxTeams), maxTeams, playersPerTeam, roomFullMessage);
        occupancy = new int[Mathf.Max(1, TeamCount)];
    }

    // --- 플랫폼 ---

    /// 백엔드를 갈아 끼운다. 방 안이거나 접속 중이면 거절한다 — 도중에 바꾸면 남들 화면에
    /// 내가 남은 채로 사라진다.
    public bool SwitchPlatform(NetPlatform platform)
    {
        if (Platform == platform) return true;

        var manager = NetworkManager.Singleton;
        if (InRoom || (manager != null && manager.IsListening))
        {
            Fail("방을 나간 뒤에 플랫폼을 바꾼다.");
            return false;
        }

        UseBackend(platform);
        backend.Initialize();

        rooms.Clear();
        Status = backend.LastError;
        Changed?.Invoke();
        return true;
    }

    void UseBackend(NetPlatform platform)
    {
        if (backend != null)
        {
            backend.Changed -= OnBackendChanged;
            backend.MatchStarted -= OnMatchStarted;
            backend.Shutdown();
        }

        Platform = platform;
        backend = platform == NetPlatform.Steam
            ? new SteamLobbyBackend(steamAppId)
            : (ILobbyBackend)new LocalLobbyBackend();

        backend.Changed += OnBackendChanged;
        backend.MatchStarted += OnMatchStarted;
    }

    /// 방의 무언가가 바뀌었다. 누가 들어오고 나갔든, 남이 팀·준비·픽을 눌렀든 여기로 온다.
    void OnBackendChanged()
    {
        RefreshMembers();
        Changed?.Invoke();
    }

    /// 로비 세션을 연다. `GameManager`의 부팅 사슬이 부르며, Awake에서 스스로 열지 않는다 —
    /// 무엇이 언제 초기화되는지를 호출 순서가 아니라 코드 한 줄로 읽게 하기 위해서다.
    ///
    /// Facepunch의 `SteamClient.Init`은 동기다. 그래도 한 프레임 양보하고 여는 이유는,
    /// 스팀이 꺼져 있을 때 예외가 나기까지 걸리는 시간이 그대로 씬 첫 프레임을 붙잡기
    /// 때문이다. 여기서 기다리는 것은 스팀이 아니라 프레임이다.
    public async UniTask InitializeAsync()
    {
        await UniTask.Yield();

        backend.Initialize();
        Status = backend.LastError;
        if (!string.IsNullOrEmpty(Status)) CDebug.LogWarning($"{name}: {Status}", this);
    }

    // NetworkManager는 이 오브젝트보다 늦게 깨어날 수 있다. Awake 순서에 기대지 않는다.
    void OnEnable() => SubscribeToNetwork();

    void Start() => SubscribeToNetwork();

    void OnDisable() => UnsubscribeFromNetwork();

    /// 백엔드 펌프. 스팀은 콜백을, 로컬 로비는 다른 창의 기록을 여기서 본다. 이것이 없으면
    /// 남이 무엇을 했는지 영영 알 수 없어, 매 프레임이어야 하는 몇 안 되는 처리다.
    void Update() => backend?.Pump();

    void OnDestroy()
    {
        alive = false;
        if (backend == null) return;

        backend.Changed -= OnBackendChanged;
        backend.MatchStarted -= OnMatchStarted;
        backend.LeaveRoom();
        backend.Shutdown();
        backend = null;
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

        // 에디터 플랫폼은 스팀 트랜스포트를 쓰지 않으므로 없어도 문제가 없다.
        if (steamTransport == null && Platform == NetPlatform.Steam)
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

    // --- 방 목록 ---

    public async Task RefreshRoomsAsync()
    {
        if (!Guard()) return;

        Status = "방 목록을 받는 중…";
        Changed?.Invoke();

        var found = await backend.ListRoomsAsync(roomListLimit);
        if (!alive) return;

        if (found == null)
        {
            Fail(backend.LastError);
            return;
        }

        rooms.Clear();
        rooms.AddRange(found);

        Status = rooms.Count > 0 ? string.Empty : "방이 없다.";
        Changed?.Invoke();
    }

    // --- 방 만들기 / 참가 ---

    /// 방을 만들고 대기실로 들어간다. 네트워크는 아직 뜨지 않는다 — 팀을 고르고 인원을
    /// 확인하는 동안은 로비만으로 충분하고, 접속을 먼저 열면 대기실을 나가는 것과
    /// 매치를 나가는 것이 같은 일이 돼 버린다.
    ///
    /// `teamCount`는 로비 UI가 만들기 전에 고른 값이다 (기획서 10장: 2/3/4팀). 정원
    /// (`RoomCapacity`)이 이 값으로 정해지므로 방을 만들기 *전에* 먼저 반영해야 한다.
    public async Task<bool> CreateRoomAsync(string roomName, int teamCount)
    {
        if (!Guard()) return false;

        ReconfigureSeating(teamCount);

        Status = "방을 만드는 중…";
        Changed?.Invoke();

        var created = await backend.CreateRoomAsync(roomName, RoomCapacity, TeamCount);
        if (!alive) return false;

        if (!created)
        {
            Fail(backend.LastError);
            return false;
        }

        EnterRoom();
        return true;
    }

    /// 방에 들어가 대기실을 연다. 접속은 호스트가 시작을 누를 때다.
    public async Task<bool> JoinRoomAsync(LobbyRoom room)
    {
        if (!Guard()) return false;

        if (room.HostId == 0)
        {
            Fail("방의 호스트를 알 수 없다.");
            return false;
        }

        Status = "방에 들어가는 중…";
        Changed?.Invoke();

        var entered = await backend.JoinRoomAsync(room);
        if (!alive) return false;

        if (!entered)
        {
            Fail(backend.LastError);
            return false;
        }

        EnterRoom();
        return true;
    }

    void EnterRoom()
    {
        Status = string.Empty;

        // 참가자는 이 방의 팀 수를 몰랐다 — 자기 씬의 기본값(`teams`)이 아니라 방을 만든
        // 사람이 실제로 고른 값을 읽어야 한다. 값이 없거나(옛 방·다른 게임) 못 읽으면
        // 씬 기본값으로 되돌린다.
        var teamCount = backend.RoomTeamCount;
        ReconfigureSeating(teamCount > 0 ? teamCount : teams);

        // 내 팀을 바로 적어 둬야 남들 화면의 인원 표시에 내가 잡힌다.
        SelectTeam(FirstTeamWithRoom());
    }

    // 결과 화면은 네트워크만 종료하고 로비 멤버십은 보존한다 (기획서 4.3).
    public void ReturnToRoom()
    {
        var manager = NetworkManager.Singleton;
        if (manager != null && (manager.IsListening || manager.IsClient)) manager.Shutdown();
        else OnNetworkStopped(false);
    }

    public void LeaveRoom()
    {
        backend?.LeaveRoom();
        members.Clear();
        ClearOccupancy();
        selfReady = false;
        pendingServer = 0;

        var manager = NetworkManager.Singleton;
        if (manager != null && (manager.IsListening || manager.IsClient)) manager.Shutdown();

        Changed?.Invoke();
    }

    // --- 대기실 ---

    /// 준비를 켜고 끈다. 팀과 같은 통로(멤버 데이터)로 나가므로 같은 방의 모두가 즉시 본다.
    /// 방장은 시작 버튼을 쥐고 있어 준비 개념이 없다.
    public void ToggleReady()
    {
        if (!InRoom || IsRoomHost) return;

        selfReady = !selfReady;
        WriteSelf();

        RefreshMembers();
        Changed?.Invoke();
    }

    /// 팀을 고른다. 로비 멤버 데이터로 적히므로 같은 방의 모두가 즉시 본다.
    public void SelectTeam(int team)
    {
        if (team < 0 || team >= TeamCount) return;

        SelectedTeam = team;
        WriteSelf();

        // 내 변경은 콜백을 기다리지 않고 바로 반영한다. 스팀은 자기 변경을 되돌려 주지
        // 않을 수도 있고, 그러면 내 선택만 화면에서 한 박자 늦는다.
        RefreshMembers();
        Changed?.Invoke();
    }

    /// 팀·준비·픽은 언제나 함께 나간다. 셋을 따로 쓰면 어느 하나가 빠진 채로 남들 화면에
    /// 그려지는 순간이 생긴다.
    void WriteSelf()
    {
        if (backend != null && backend.InRoom)
            backend.WriteSelf(SelectedTeam, selfReady, SelectedCharacter);
    }

    void RefreshMembers()
    {
        members.Clear();
        ClearOccupancy();
        if (backend == null || !backend.InRoom) return;

        var ownerId = backend.RoomHostId;
        var selfId = backend.SelfId;
        var read = backend.ReadMembers();

        for (var i = 0; i < read.Count; i++)
        {
            var member = read[i];
            var isSelf = member.Id == selfId;
            var isHost = member.Id == ownerId;
            var team = isSelf ? SelectedTeam : member.Team;

            // 내 값은 멤버 데이터를 되읽지 않는다. 스팀이 방금 쓴 값을 곧바로 돌려준다는
            // 보장이 없어서, 눌렀는데 한 박자 뒤에야 켜지는 것처럼 보인다 (팀과 같은 이유).
            var ready = isSelf ? selfReady : member.Ready;
            var pick = isSelf ? SelectedCharacter : member.Character;

            if (team >= 0 && team < occupancy.Length) occupancy[team]++;

            members.Add(new RoomMember(member.Id, member.Name, team,
                                       isSelf, isHost, ready, pick));
        }
    }

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
    /// 손님은 `AnnounceServer`가 일으키는 신호를 받고 붙는다.
    public bool StartMatch()
    {
        if (!Guard() || !InRoom) return false;
        if (!IsRoomHost)
        {
            Fail("방장만 시작할 수 있다.");
            return false;
        }

        if (!CanStartMatch) { Fail("모든 참가자의 준비와 팀 정원을 확인해 주세요."); return false; }
        if (mapScenes != null && mapScenes.Length > 0) gameScene = mapScenes[UnityEngine.Random.Range(0, mapScenes.Length)];
        backend.CloseRoom();
        Seating.ExpectPlayers(members.Count);

        if (!StartNetwork(SelectedTeam, host: true, targetSteamId: 0)) { backend.ReopenRoom(); return false; }

        // 손님에게 "여기로 붙어라"를 알린다. 서버가 뜬 뒤여야 한다 — 게임 씬은
        // `LoadGameSceneServer`가 서버 기동 이벤트에서 이미 걸었다.
        backend.AnnounceServer();
        return true;
    }

    /// 서버가 뜨면 게임 씬으로 넘어간다. 방에서 시작했든 개발 콘솔의 Host 버튼을 눌렀든
    /// 같은 곳으로 가야 한다 — 여기 말고 방 흐름 안에만 두면 로비 없이 여는 로컬 테스트가
    /// 타이틀에 갇힌다.
    void LoadGameSceneServer()
    {
        var manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer) return;
        if (IsGameScene(SceneManager.GetActiveScene().name)) return;

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

    /// 방장이 매치를 열었다. 붙을 곳이 방장인지는 백엔드가 이미 확인했다.
    void OnMatchStarted(ulong serverId)
    {
        // 이미 떠 있으면 내가 그 방장이다. 스팀은 같은 계정으로 창을 두 개 띄우면 방장에게도
        // 이 신호가 돌아와서, 계정이 아니라 "내가 서버인가"로 걸러야 한다.
        var manager = NetworkManager.Singleton;
        if (manager != null && manager.IsListening) return;

        // 바로 붙지 않는다. 기획서 10.1의 순서가 방 → 캐릭터 선택 → 매치라서, 손님도
        // 여기서 선택 화면을 먼저 보고 「확정」할 때 `JoinStartedMatch`로 붙는다.
        pendingServer = serverId;
        MatchStarting?.Invoke();
    }

    /// 방장이 매치를 열었다. 손님 화면이 캐릭터 선택으로 넘어가는 신호다 (기획서 10.1).
    /// 방장 자신은 위에서 걸러지므로 여기서 오르지 않는다 — 방장은 시작 버튼이 곧 이 신호다.
    public event Action MatchStarting;

    /// 붙을 곳. `OnMatchStarted`가 방 주인인지까지 검증한 뒤에만 채운다.
    ulong pendingServer;

    /// 손님 전용. 캐릭터 선택에서 「확정」을 누르면 그때 붙는다.
    ///
    /// 픽이 접속보다 먼저 있어야 한다 — `PlayerCharacter.OnNetworkSpawn`이
    /// `GameManager.SelectedCharacter`를 읽어 서버로 올리기 때문이다.
    public bool JoinStartedMatch()
    {
        if (pendingServer == 0) return false;

        var target = pendingServer;
        pendingServer = 0;
        return StartNetwork(SelectedTeam, host: false, target);
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
        manager.NetworkConfig.ConnectionData = MatchSeating.EncodeTeamRequest(team, SelectedCharacter);

        // 에디터 플랫폼은 스팀을 아예 켜지 않으므로 로컬 트랜스포트로 붙는다 — 같은 PC의
        // 창끼리라 상대 주소가 필요 없다.
        var useSteam = Platform == NetPlatform.Steam && steamTransport != null;
        var transport = useSteam ? steamTransport : localTransport;
        if (transport == null)
        {
            Fail("쓸 수 있는 트랜스포트가 없다.");
            return false;
        }

        manager.NetworkConfig.NetworkTransport = transport;
        if (useSteam) steamTransport.TargetSteamId = targetSteamId;

        var started = host ? manager.StartHost() : manager.StartClient();
        if (!started) Fail(host ? "호스트를 시작하지 못했다." : "접속을 시작하지 못했다.");
        return started;
    }

    /// 매치 연결을 정리하고 기존 방의 대기 화면으로 돌아간다.
    /// 명시적 탈퇴는 LeaveRoom이 먼저 멤버십을 정리한다.
    void OnNetworkStopped(bool _)
    {
        if (InRoom) backend.ReopenRoom();
        members.Clear();
        ClearOccupancy();
        selfReady = false;
        pendingServer = 0;

        var manager = NetworkManager.Singleton;
        if (manager != null && localTransport != null)
            manager.NetworkConfig.NetworkTransport = localTransport;

        // 좌석표는 런처와 함께 살아남으므로 씬을 다시 불러도 저절로 비지 않는다.
        Seating?.ResetForNewMatch();

        WriteSelf();
        RefreshMembers();
        Changed?.Invoke();

        if (SceneManager.GetActiveScene().name != titleScene) SceneManager.LoadScene(titleScene);
    }

    void Fail(string reason)
    {
        Status = string.IsNullOrEmpty(reason) ? "알 수 없는 이유로 실패했다." : reason;

        Changed?.Invoke();
    }

    bool Guard()
    {
        if (Ready) return true;

        if (string.IsNullOrEmpty(Status))
            Status = backend != null && !string.IsNullOrEmpty(backend.LastError)
                ? backend.LastError
                : "로비가 준비되지 않았다.";

        WriteSelf();
        RefreshMembers();
        Changed?.Invoke();
        return false;
    }
}
