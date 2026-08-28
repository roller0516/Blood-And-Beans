/// 갱신 한 번 동안 그룹들이 함께 보는 상태 스냅샷.
///
/// 그룹마다 `NetworkManager.Singleton`이나 싱글턴을 다시 조회하면, 그룹이 늘어날수록 같은
/// 조회가 배로 늘고 그룹끼리 서로 다른 순간의 값을 볼 수도 있다. 창이 한 번 읽어 넘긴다.
public readonly struct DevConsoleState
{
    /// 재생 중인가. 재생 전에는 씬 오브젝트가 없어 대부분의 조작이 뜻을 갖지 않는다.
    public readonly bool Playing;

    public readonly bool Listening;
    public readonly bool IsServer;

    /// 매치 시계. 매치 씬이 아직 로드되지 않았으면 null이다.
    public readonly GamePhase Clock;

    /// 좌석 권위. 런처 씬이 깨어나기 전에는 null이다.
    public readonly MatchSeating Seating;

    public DevConsoleState(bool playing, bool listening, bool isServer,
                           GamePhase clock, MatchSeating seating)
    {
        Playing = playing;
        Listening = listening;
        IsServer = isServer;
        Clock = clock;
        Seating = seating;
    }

    /// 시계가 살아 있고 판이 아직 안 끝났는가.
    /// Unity 오브젝트에 `?.`를 쓰면 파괴된 인스턴스를 살아 있는 것으로 통과시키므로,
    /// Unity가 재정의한 `==`를 타도록 명시적으로 검사한다.
    public bool ClockRunning => Clock != null && Clock.IsSpawned;

    public bool ClockUsable => ClockRunning && !Clock.Finished;
}
