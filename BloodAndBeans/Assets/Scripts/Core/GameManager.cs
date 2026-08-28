using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;

/// 씬을 넘어 살아남는 영속 오브젝트이자 부팅 순서의 주인. 접속(`NetworkManager`),
/// 좌석 배정(`MatchSeating`), 방(`SteamLobby`)이 이 오브젝트에 함께 있고, 원본은
/// `Assets/Resources/GameManager.prefab` 하나뿐이다. 어느 씬에서 시작하든 스스로 붙는다.
///
/// 예전에는 이들이 런처 씬에 놓여 있었다. 그래서 런처를 거치지 않고 시작하면 — 에디터에서
/// 전투 씬을 열어 두고 재생하는 경우가 그렇다 — 통째로 없었고, 접속도 좌석 배정도 시작되지
/// 않아 플레이어조차 스폰되지 않았다. `EditorSceneManager.playModeStartScene`으로 우회하면
/// 에디터 설정 하나가 게임의 부팅 조건이 되어, 그 값이 비어 있는 환경에서 같은 증상이
/// 되돌아온다.
///
/// 게임 규칙은 여기 두지 않는다. 이 클래스가 아는 것은 "무엇이 어떤 순서로 준비되는가"뿐이고,
/// 한 판의 규칙과 시계는 전투 씬의 `MatchDirector`와 `GamePhase`가 그대로 맡는다.
public class GameManager : PersistentMonoSingleton<GameManager>
{
    /// `Assets/Resources/` 아래의 프리팹 이름. 타입 이름과 같아야 한다.
    const string PrefabName = nameof(GameManager);

    /// 접속 계층의 프리팹. 게임매니저와 **다른 프리팹**이다.
    ///
    /// 프리팹의 루트는 하나뿐인데 NGO가 `NetworkManager`의 중첩을 금지한다
    /// (`"NetworkManager cannot be nested."`). 한 프리팹에 두면 둘 중 하나는 반드시
    /// 상대의 자식이 되므로, 서로 독립시키려면 프리팹부터 갈라야 한다.
    const string NetworkPrefabName = "NetworkManager";

    /// 같은 오브젝트의 방·세션. 부팅 사슬이 깨우는 대상이다.
    [SerializeField] SteamLobby lobby;

    /// 부팅이 끝났는가. 타이틀은 이 뒤에 열려야 방 목록이 "스팀이 준비되지 않았다"로
    /// 한 번 깜빡였다가 고쳐지지 않는다.
    public bool IsReady { get; private set; }

    /// 이 판의 좌석 권위. 소유자는 `SteamLobby`이고 여기서는 통로만 연다 — 플레이어와
    /// 매치 씬은 로비를 직접 알 이유가 없고, 세션 단위로 하나뿐인 것을 찾을 자리는 여기다.
    public static MatchSeating Seating =>
        Instance != null && Instance.lobby != null ? Instance.lobby.Seating : null;

    /// `BeforeSceneLoad`는 어떤 씬 오브젝트의 `Awake`보다도 먼저 돈다. 그래서 `MatchDirector`가
    /// `Awake`에서 `MatchSeating`을 찾는 것이 실행 순서에 기대지 않고 항상 성립한다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void CreateIfMissing()
    {
        // 접속 계층이 먼저다. `SteamLobby`는 깨어나면서 `NetworkManager.Singleton`을 찾고,
        // 좌석표의 접속 승인은 누가 붙기 전에 걸려 있어야 한다.
        Create(NetworkPrefabName, NetworkManager.Singleton != null);
        Create(PrefabName, Instance != null);
    }

    /// 씬에 손으로 놓아 둔 것이 있으면 두 번 만들지 않는다.
    /// 싱글턴 등록과 `DontDestroyOnLoad`는 각 프리팹 루트의 컴포넌트가 알아서 한다.
    static void Create(string prefabName, bool alreadyExists)
    {
        if (alreadyExists) return;

        var prefab = Resources.Load<GameObject>(prefabName);
        if (prefab == null)
        {
            CDebug.LogError($"Resources/{prefabName}을 찾을 수 없다. 그 계층 없이 시작한다.");
            return;
        }

        // "(Clone)"을 떼어 로그에서 프리팹과 같은 이름으로 보이게 한다.
        Instantiate(prefab).name = prefab.name;
    }

    protected override void Awake()
    {
        base.Awake();

        // 중복이면 기반 클래스가 이 오브젝트를 파괴하기로 했다. 사라질 오브젝트가 부팅을
        // 시작하면 스팀 세션이 두 번 열린다.
        if (Instance != this) return;

        Boot().Forget();
    }

    /// 부팅 순서를 한 곳에 모은다. `Awake`·`OnEnable`·`Start`의 암묵적 호출 순서에 기대면
    /// 컴포넌트를 하나 더 붙이는 날 순서가 조용히 바뀐다.
    ///
    /// 지금 실제로 기다리는 것은 스팀 한 단계뿐이다. `NetworkManager`는 자기 `Awake`에서
    /// 동기로 서고, 좌석표도 마찬가지다 — 없는 대기를 만들어 붙이지 않았다. 뒤에 진짜
    /// 비동기 단계(로그인 대기, 설정 로드 등)가 생기면 이 사슬에 한 줄 더한다.
    async UniTaskVoid Boot()
    {
        if (lobby != null) await lobby.InitializeAsync();
        else CDebug.LogError($"{name}: {nameof(lobby)}가 비어 있다. 스팀 없이 시작한다.", this);

        IsReady = true;
    }
}
