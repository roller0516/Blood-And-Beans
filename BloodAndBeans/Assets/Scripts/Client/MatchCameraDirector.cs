using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

/// 매치 카메라의 조립 지점. 실제 카메라는 하나뿐이고(`CinemachineBrain`), 어디를 비출지는
/// 가상 카메라 둘 중 우선순위가 높은 쪽이 정한다.
///
/// **페이즈와 무관하게 플레이어를 따라간다.** 밤에는 숲을 걷고, 낮·전환에는 카페 안에
/// 서 있다 (`PlayerTeam.MoveToPhaseStartServer`) — 어느 쪽이든 화면 한가운데 내 캐릭터가
/// 있다. 카페에 고정된 부감 뷰를 따로 두었었지만, 카페에서도 캐릭터를 움직이는 이상
/// 시점이 페이즈마다 뒤바뀔 이유가 없다.
///
/// 따라가는 방식만 둘이고(`PlayerView`) 개발 콘솔에서 갈아 끼운다 — 쿼터뷰와 TPP의 느낌을
/// 눈으로 비교하려는 장치이지 게임 규칙이 아니다.
///
/// 카페가 검게 보이는 것은 카메라가 아니라 지오메트리 문제다. 카페는 숲 밖 빈 공간에 서
/// 있어서 주변에 아무것도 없고 카메라 배경색(검정)만 남는다. 동물의 숲 텐트 실내와 같은
/// 그림이며, 그 검정의 근거는 `MatchDirector.cafeAreaGap`이 벌려 둔 거리다.
///
/// 카메라 한 대에 분기를 넣지 않는다. 추적 방식이 둘 다 앞으로 손댈 값이고, 우선순위만
/// 바꾸면 전환과 블렌딩은 브레인이 맡는다.
public class MatchCameraDirector : MonoBehaviour
{
    /// 플레이어를 따라다니는 카메라의 시점. 모든 페이즈에 같은 시점을 쓴다.
    public enum PlayerView
    {
        /// 위에서 비스듬히 내려다보는 시점. 멀리서 좁은 화각으로 잡아 원근을 누른다.
        Quarter,

        /// 어깨 너머. 같은 판을 눈높이에서 보면 거리감과 안개가 전혀 다르게 읽힌다.
        /// 기본값이다.
        ThirdPerson,
    }

    [Header("가상 카메라")]
    /// 쿼터뷰. 필드 이름은 씬 배선(GUID가 아니라 필드 이름으로 붙는다)을 지키려고 그대로 둔다.
    [SerializeField] CinemachineCamera nightCamera;

    /// TPP. 기본 시점이다. 비어 있으면 시점 전환이 없는 것으로 보고 쿼터뷰만 쓴다.
    [SerializeField] CinemachineCamera tppCamera;

    /// 브레인은 우선순위가 높은 쪽을 따른다.
    [SerializeField] int activePriority = 20;
    [SerializeField] int idlePriority = 0;

    [Header("페이즈 전환")]
    /// 페이즈가 바뀔 때 카메라를 블렌딩 없이 잘라 붙인 뒤, 이 시간 동안 매 프레임 다시
    /// 붙여 둔다. 페이즈 복제와 순간이동이 같은 프레임에 도착한다는 보장이 없어서
    /// 한 번만 자르면 늦게 도착한 순간이동만큼 카메라가 뒤따라 날아온다. 숲과 카페는
    /// `cafeAreaGap`만큼 떨어져 있어서 낮으로 넘어갈 때도 같은 문제가 그대로 있다.
    /// 이 창 동안 캐릭터는 아직 디졸브 중이라 감쇠가 없어도 화면에 티가 나지 않는다
    /// (`PlayerDissolve`).
    [SerializeField] float phaseSnapSeconds = 0.3f;

    [Header("가시성")]
    /// 컬링 마스크를 깎을 실제 카메라. 브레인이 붙어 있는 그 카메라다.
    [SerializeField] Camera view;

    NetworkObject player;
    PlayerTeam team;
    MatchDirector director;
    GamePhase clock;

    /// 카메라를 목표 자리에 붙여 두는 창의 끝 시각. 지났으면 시네머신이 평소대로 감쇠한다.
    float snapUntil;

    /// 지금 고른 시점. 바꾸는 것은 개발 콘솔뿐이다.
    public PlayerView View { get; private set; } = PlayerView.ThirdPerson;

    /// 시점을 바꾼다. 어느 페이즈에서 불러도 그 자리에서 바로 바뀐다.
    public void SetView(PlayerView view)
    {
        if (View == view) return;

        View = view;
        ApplyPriorities();
    }

    public void ToggleView() =>
        SetView(View == PlayerView.Quarter ? PlayerView.ThirdPerson : PlayerView.Quarter);

    void OnDisable() => Unbind();

    /// 늦게 생기는 참조(로컬 플레이어)를 잡을 때까지만 돈다. 잡히면 이 함수는 첫 줄에서
    /// 끝난다 (AGENTS.md 참조와 결합도).
    void Update()
    {
        if (player == null)
        {
            var manager = NetworkManager.Singleton;
            var local = manager != null && manager.IsClient && manager.LocalClient != null
                ? manager.LocalClient.PlayerObject
                : null;
            if (local == null) return;

            Bind(local);
            return;
        }

        // 가상 카메라들의 이전 프레임 상태를 버리고 브레인이 블렌딩 없이 새 카메라를
        // 고르게 한다. 브레인은 LateUpdate에 도므로 여기서 무효화하면 이번 프레임부터 먹는다.
        if (Time.time < snapUntil) CinemachineCore.ResetCameraState();
    }

    void Bind(NetworkObject local)
    {
        player = local;

        // 궤도 카메라는 도는 중심(Follow)과 보는 곳(LookAt)이 둘 다 있어야 한다.
        // LookAt이 비면 회전 구성기가 기준을 잃고 카메라가 한 방향만 본다.
        FollowPlayer(nightCamera, local.transform);
        FollowPlayer(tppCamera, local.transform);

        team = local.GetComponent<PlayerTeam>();
        if (team != null)
        {
            team.TeamChanged += OnTeamChanged;
            OnTeamChanged(team.Team);
        }

        // 매치 씬은 플레이어보다 늦게 설 수 있다. 이미 서 있으면 지금, 아니면 설 때 불린다.
        MatchDirector.Bind(OnDirectorReady);
    }

    static void FollowPlayer(CinemachineCamera camera, Transform target)
    {
        if (camera == null) return;

        camera.Follow = target;
        camera.LookAt = target;
    }

    void Unbind()
    {
        if (team != null) team.TeamChanged -= OnTeamChanged;
        if (clock != null) clock.PhaseEntered -= OnPhaseEntered;
        MatchDirector.Unbind(OnDirectorReady);

        team = null;
        clock = null;
        director = null;
        player = null;
    }

    /// 같은 인스턴스로 두 번 불릴 수 있다 (`MatchDirector.Bind` 계약).
    void OnDirectorReady(MatchDirector ready)
    {
        if (director == ready) return;

        if (clock != null) clock.PhaseEntered -= OnPhaseEntered;
        director = ready;
        clock = ready != null ? ready.Phase : null;

        if (clock == null) return;
        clock.PhaseEntered += OnPhaseEntered;
        OnPhaseEntered(clock.Current);
    }

    /// 팀이 정해져야 볼 수 있는 것이 정해진다. 예전에는 이 적용이 카메라 추적과 같은
    /// `LateUpdate`에 있어서, 한 번 하면 끝날 일이 매 프레임 조건문 두 개로 남아 있었다.
    void OnTeamChanged(int myTeam)
    {
        if (view != null && director != null)
            TeamVision.ApplyServer(view, myTeam, director.TeamCount);
    }

    void OnPhaseEntered(Phase phase)
    {
        // 팀 가시성은 카페가 스폰된 뒤에야 완성된다. 페이즈가 바뀔 때 한 번 더 맞춘다.
        if (view != null && team != null && director != null)
            TeamVision.ApplyServer(view, team.Team, director.TeamCount);

        // 밤이든 낮이든 페이즈가 바뀌면 플레이어가 맵 반대편으로 순간이동한다.
        snapUntil = Time.time + phaseSnapSeconds;

        ApplyPriorities();
    }

    /// 브레인이 따를 카메라를 정한다. 고른 시점 하나만 살리고 나머지는 재운다.
    void ApplyPriorities()
    {
        // TPP 카메라가 없으면 쿼터뷰가 통째로 맡는다.
        var thirdPerson = View == PlayerView.ThirdPerson && tppCamera != null;

        if (nightCamera != null) nightCamera.Priority = thirdPerson ? idlePriority : activePriority;
        if (tppCamera != null) tppCamera.Priority = thirdPerson ? activePriority : idlePriority;
    }
}
