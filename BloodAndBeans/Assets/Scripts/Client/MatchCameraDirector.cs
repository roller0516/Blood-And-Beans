using Unity.Cinemachine;
using Unity.Netcode;
using UnityEngine;

/// 매치 카메라의 조립 지점. 실제 카메라는 하나뿐이고(`CinemachineBrain`), 어디를 비출지는
/// 페이즈에 따라 가상 카메라 둘이 번갈아 정한다.
///
/// - **밤**: 플레이어를 따라간다. 숲을 걸어 다니는 화면이다. 따라가는 방식은 둘이고
///   (`PlayerView`) 개발 콘솔에서 갈아 끼운다 — 쿼터뷰와 TPP의 느낌을 눈으로 비교하려는
///   장치이지 게임 규칙이 아니다.
/// - **낮·전환**: 내 팀 카페에 고정된다. 카페는 숲 밖 빈 공간에 서 있어서, 카메라가 거기
///   가면 주변에 지오메트리가 하나도 없고 카메라 배경색(검정)만 남는다. 동물의 숲 텐트
///   실내와 같은 그림이며, 마스크나 후처리가 아니라 "거기 아무것도 없다"로 만든다.
///   그 검정의 근거는 `MatchDirector.cafeAreaGap`이 벌려 둔 거리다.
///
/// 카메라 한 대에 분기를 넣지 않는다. 추적 방식이 둘 다 앞으로 손댈 값이고, 우선순위만
/// 바꾸면 전환과 블렌딩은 브레인이 맡는다.
public class MatchCameraDirector : MonoBehaviour
{
    /// 플레이어를 따라다니는 카메라의 시점. 카페 고정 뷰는 이 선택과 무관하다.
    public enum PlayerView
    {
        /// 위에서 비스듬히 내려다보는 시점. 멀리서 좁은 화각으로 잡아 원근을 누른다.
        Quarter,

        /// 어깨 너머. 같은 판을 눈높이에서 보면 거리감과 안개가 전혀 다르게 읽힌다.
        /// 기본값이다.
        ThirdPerson,
    }

    [Header("가상 카메라")]
    /// 밤 · 쿼터뷰.
    [SerializeField] CinemachineCamera nightCamera;

    /// 밤 · TPP. 기본 시점이다. 비어 있으면 시점 전환이 없는 것으로 보고 쿼터뷰만 쓴다.
    [SerializeField] CinemachineCamera tppCamera;

    /// 낮 · 전환. 내 팀 카페에 고정된다.
    [SerializeField] CinemachineCamera dayCamera;

    /// 브레인은 우선순위가 높은 쪽을 따른다.
    [SerializeField] int activePriority = 20;
    [SerializeField] int idlePriority = 0;

    [Header("낮 카페 뷰")]
    /// 카페 중심에서 카메라가 서는 자리. 밤 카메라의 Follow Offset과 같은 뜻이다.
    [SerializeField] Vector3 cafeOffset = new(0f, 14f, -8f);

    [Header("가시성")]
    /// 컬링 마스크를 깎을 실제 카메라. 브레인이 붙어 있는 그 카메라다.
    [SerializeField] Camera view;

    NetworkObject player;
    PlayerTeam team;
    MatchDirector director;
    GamePhase clock;

    /// 마지막으로 들어간 페이즈. 시점을 바꿀 때 "지금 따라다니는 중인가"를 다시 물어볼
    /// 곳이 없어서 들고 있는다.
    Phase currentPhase = Phase.Night;

    /// 낮 카메라가 카페 위에 자리를 잡았는가. 카페는 팀 배정보다 늦게 복제될 수 있어서,
    /// 페이즈 전환 한 번에만 기대면 그 순간 카페가 없던 클라이언트는 낮 카메라가 원점에
    /// 선 채로 남는다 — 화면이 카페도 플레이어도 아닌 곳을 비춘다.
    bool dayCameraPlaced;

    /// 지금 고른 시점. 바꾸는 것은 개발 콘솔뿐이다.
    public PlayerView View { get; private set; } = PlayerView.ThirdPerson;

    /// 시점을 바꾼다. 낮·전환처럼 카페에 고정된 동안 불러도 되며, 밤이 되면 고른 쪽이 뜬다.
    public void SetView(PlayerView view)
    {
        if (View == view) return;

        View = view;
        ApplyPriorities();
    }

    public void ToggleView() =>
        SetView(View == PlayerView.Quarter ? PlayerView.ThirdPerson : PlayerView.Quarter);

    void OnDisable() => Unbind();

    /// 늦게 생기는 두 참조(로컬 플레이어, 내 팀 카페)를 잡을 때까지만 돈다. 둘 다 잡히면
    /// 이 함수는 첫 두 줄에서 끝난다 (AGENTS.md 참조와 결합도).
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

        if (!dayCameraPlaced) PlaceDayCamera();
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
        dayCameraPlaced = false;
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

        // 팀이 바뀌면 비출 카페도 바뀐다. 자리를 다시 잡게 한다.
        dayCameraPlaced = false;
        PlaceDayCamera();
    }

    void OnPhaseEntered(Phase phase)
    {
        // 팀 가시성은 카페가 스폰된 뒤에야 완성된다. 페이즈가 바뀔 때 한 번 더 맞춘다.
        if (view != null && team != null && director != null)
            TeamVision.ApplyServer(view, team.Team, director.TeamCount);

        currentPhase = phase;
        if (phase != Phase.Night) PlaceDayCamera();

        ApplyPriorities();
    }

    /// 브레인이 따를 카메라를 정한다. 밤이면 고른 시점의 추적 카메라, 아니면 카페 고정.
    ///
    /// 페이즈와 시점 두 축이 여기 한 곳에서만 만난다. 두 군데서 우선순위를 만지면
    /// 시점을 바꾼 뒤 페이즈가 넘어갈 때 어느 쪽이 이겼는지 알 수 없게 된다.
    void ApplyPriorities()
    {
        var following = currentPhase == Phase.Night;

        // TPP 카메라가 없으면 쿼터뷰가 밤을 통째로 맡는다.
        var thirdPerson = following && View == PlayerView.ThirdPerson && tppCamera != null;
        var quarter = following && !thirdPerson;

        if (nightCamera != null) nightCamera.Priority = quarter ? activePriority : idlePriority;
        if (tppCamera != null) tppCamera.Priority = thirdPerson ? activePriority : idlePriority;
        if (dayCamera != null) dayCamera.Priority = following ? idlePriority : activePriority;
    }

    /// 낮 카메라를 내 팀 카페 위에 세운다. 카페가 아직 복제되지 않았으면 아무것도 하지
    /// 않고, `Update`가 잡힐 때까지 다시 시도한다.
    void PlaceDayCamera()
    {
        if (dayCamera == null || director == null || team == null) return;

        var cafe = director.CafeOf(team.Team);
        if (cafe == null) return;

        dayCamera.transform.position = cafe.transform.position + cafeOffset;
        dayCameraPlaced = true;
    }
}
