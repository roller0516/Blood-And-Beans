using Unity.Netcode;
using UnityEngine;

public enum Judgement { Perfect, Good, Miss, Burnt }

/// 제작이 끝난 뒤 움직이는 바늘 (기획서 5.2). 자기 Station과 같은 GameObject에 붙는다.
///
/// 바늘 위치는 동기화하지 않는다. 서버 시간의 순수 함수이므로 모든 클라이언트가 같은
/// 바늘을 그리고, 서버는 어떤 클라이언트가 F를 누른 정확한 순간을 판정할 수 있다.
public class CompletionGauge : NetworkBehaviour
{
    [SerializeField] float window = 10f;        // 이 시간 동안 완성 판정을 칠 수 있다

    [SerializeField] float sweepsPerSecond = 1.4f;
    [SerializeField] float perfectHalfWidth = 0.05f;   // 중앙 0.5로부터의 거리
    [SerializeField] float goodHalfWidth = 0.16f;
    // ponytail: 팀원 보조 거리는 설비 기본 사거리와 같게 시작한다. 레벨 확정 시 조정한다.
    [SerializeField] float assistReach = 2.5f;

    readonly NetworkVariable<bool> active = new();
    readonly NetworkVariable<double> startedAt = new();

    /// 이번 게이지의 유예. 기본은 `window`(기획서 5.2의 10초)이고, 팀에 「제빵사」가
    /// 있으면 20초다 (기획서 9.1). 클라이언트도 남은 시간을 그리므로 복제한다.
    readonly NetworkVariable<float> windowNow = new(10f);

    /// 서버 전용. Station이 구독해서 제작 결과를 받는다.
    public System.Action<Judgement> OnResult;

    // 기획서 5.2: 로컬 입력은 점유자 기준이며 팀원 보조는 서버가 거리로 검증한다.
    Cafe cafe;

    /// 조립 루트는 전역이 아니라 소속 카페에서 받는다. 설비마다 따로 찾으면 카페별로
    /// 다른 답이 나올 여지가 생긴다 (아키텍처_v1.0.md §1.4).
    MatchDirector Director => cafe != null ? cafe.Director : null;

    public bool Active => active.Value;
    public Station Station => station;
    public void CancelServer() { if (IsServer) active.Value = false; }

    /// 판정 구간의 반폭. HUD가 침 뒤에 Perfect/Good 구간을 그리는 데 쓴다 (기획서 5.2).
    /// 판정과 표시가 같은 값을 읽어야 한다 — 화면에만 따로 적으면 둘이 어긋난다.
    public float PerfectHalfWidth => perfectHalfWidth * (cafe != null && cafe.HasBuff(TeamBuff.Perfect) ? DayBalance.BuffPerfect : 1f);
    public float GoodHalfWidth => goodHalfWidth;

    /// 이 게이지가 붙은 설비의 이름. HUD가 어느 기계를 판정하는지 적는 데 쓴다.
    public string StationName => name;

    public float Needle =>
        Mathf.PingPong((float)(NetworkManager.ServerTime.Time - startedAt.Value) * sweepsPerSecond, 1f);

    public float Remaining =>
        Mathf.Max(0f, windowNow.Value - (float)(NetworkManager.ServerTime.Time - startedAt.Value));

    public void BeginServer()
    {
        if (!IsServer || !IsDay) return;
        active.Value = true;
        startedAt.Value = NetworkManager.ServerTime.Time;

        windowNow.Value = window;
    }

    /// 팀 번호가 아니라 소유 카페를 들고 있는다. MatchDirector는 자기 Awake에서 팀 번호를
    /// 배정하는데 두 Awake 사이의 순서에 기대면 안 된다. 부모를 거슬러 올라가는 방식은
    /// 누가 먼저 실행되든 동작한다.
    void Awake()
    {
        cafe = Cafe.Of(this);
        station = GetComponent<Station>();
    }

    /// 같은 GameObject의 설비. 「얼음 장인」이 지금 굽고 있는 것이 찬 메뉴인지 물어본다.
    Station station;

    int TeamId => cafe != null ? cafe.TeamId : -1;
    bool IsDay => Director != null && Director.Phase.Current == Phase.Day;

    void Update()
    {
        if (IsServer && IsDay && active.Value && Remaining <= 0f) Finish(Judgement.Burnt);

    }

    /// 다음 F가 멈출 게이지. 없으면 null.
    ///
    /// 후보는 로컬 팀 카페가 캐시해 둔 목록뿐이다 (`Cafe.Gauges`). HUD가 매 프레임 부르므로
    /// 예전의 `FindObjectsByType` 전역 탐색은 여기 있을 수 없다 (AGENTS.md 참조와 결합도).
    public static CompletionGauge LocalTarget()
    {
        var director = MatchDirector.Instance;
        var team = PlayerTeam.Local();
        if (director == null || team < 0) return null;

        var cafe = director.CafeOf(team);
        if (cafe == null) return null;

        var manager = NetworkManager.Singleton;
        if (manager == null) return null;
        foreach (var g in cafe.Gauges)
            if (g != null && g.IsDay && g.station != null && g.station.OperatorId == manager.LocalClientId)
                return g.Active ? g : null;
        var player = manager.LocalClient?.PlayerObject;
        if (player == null) return null;
        CompletionGauge nearest = null;
        var distance = float.MaxValue;
        foreach (var g in cafe.Gauges)
        {
            if (g == null || !g.Active || !g.IsDay || g.station == null) continue;
            var candidate = Vector3.Distance(player.transform.position, g.station.FacilityPosition);
            if (candidate > g.assistReach || candidate >= distance) continue;
            nearest = g;
            distance = candidate;
        }
        return nearest;
    }

    public static bool TryStopLocalClient()
    {
        var target = LocalTarget();
        if (target == null) return false;

        target.StopRpc();
        return true;
    }

    /// `[Rpc(SendTo.Server)]`는 어떤 클라이언트든 호출할 수 있으므로, 팀 검사는 호출부
    /// Update뿐 아니라 반드시 여기에도 있어야 한다.
    [Rpc(SendTo.Server)]
    public void StopRpc(RpcParams p = default)
    {
        if (!IsDay || !active.Value || TeamId < 0) return;
        if (PlayerTeam.Of(p.Receive.SenderClientId) != TeamId) return;

        // 기획서 5.2: 점유자의 게이지를 멈춘다. 팀원은 설비 옆에서 도울 수 있다.
        var sender = p.Receive.SenderClientId;
        var player = Station.PlayerOf(sender);
        if (player == null || (sender != station.OperatorId &&
            Vector3.Distance(player.position, station.FacilityPosition) > assistReach)) return;
        Finish(Remaining <= 0f ? Judgement.Burnt : JudgeFor(p.Receive.SenderClientId));
    }

    void Finish(Judgement j)
    {
        active.Value = false;
        OnResult?.Invoke(j);
    }

    /// 「얼음 장인」은 `Temp.Cold` 메뉴를 항상 Perfect로 만든다 (기획서 9.1).
    /// 누른 사람의 능력이고, 지금 굽고 있는 것이 찬 메뉴인지는 설비가 안다.
    Judgement JudgeFor(ulong clientId)
    {
        var off = Mathf.Abs(Needle - 0.5f);
        var pc = PlayerCharacter.Of(clientId);
        var width = PerfectHalfWidth * (pc != null && pc.AffectedBy(2) ? 0.5f : 1f);
        return off <= width ? Judgement.Perfect : off <= goodHalfWidth ? Judgement.Good : Judgement.Miss;
    }

    Judgement Judge(float pos)
    {
        var off = Mathf.Abs(pos - 0.5f);
        return off <= perfectHalfWidth ? Judgement.Perfect
             : off <= goodHalfWidth ? Judgement.Good
             : Judgement.Miss;
    }

    /// 기획서 5.6.2. 탄 것도 여기서 0.3을 유지한다. 판매 경로가 읽을 숫자를 하나로 두기 위해서다.
    public static float MultiplierOf(Judgement j) => j switch
    {
        Judgement.Perfect => 1.3f,
        Judgement.Good => 1.0f,
        Judgement.Miss => 0.7f,
        _ => 0.3f,
    };
}
