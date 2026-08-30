using Unity.Netcode;
using UnityEngine;

public enum Phase { Night, Transition, Day }

/// 서버 권위 하루 루프: 밤 -> 전환 -> 낮을 totalDays만큼 반복한다.
/// 클라이언트는 동기화된 종료 시각에서 카운트다운을 계산하므로 네트워크로 째깍이는 값이 없다.
public class GamePhase : NetworkBehaviour
{
    [SerializeField] float nightSeconds = 120f;
    [SerializeField] float transitionSeconds = 10f;
    [SerializeField] float daySeconds = 120f;
    [SerializeField] int totalDays = 3;

    /// 귀환 경보가 켜지는 시점. 밤이 끝나기까지 남은 시간으로 잰다 — 기획서 6.4는 2분 밤의
    /// 1:30을 말하지만, 밤 길이는 `nightSeconds`로 바뀌므로 "끝나기 30초 전"이 그 뜻이다.
    [SerializeField] float returnAlarmSeconds = 30f;

    readonly NetworkVariable<Phase> phase = new();
    readonly NetworkVariable<int> day = new();
    readonly NetworkVariable<double> endsAt = new();
    readonly NetworkVariable<bool> finished = new();

    public Phase Current => phase.Value;
    public int Day => day.Value;
    public bool Finished => finished.Value;
    public float Remaining =>
        Mathf.Max(0f, (float)(endsAt.Value - NetworkManager.ServerTime.Time));
    public float Elapsed => Mathf.Max(0f, Duration(Current) - Remaining);

    /// 밤 마감 직전 구간. 경보음과 귀환 방향 표시가 여기서 켜진다 (기획서 6.4).
    ///
    /// `Remaining > 0`을 함께 보는 것은 스폰 직후 때문이다. `endsAt`이 아직 복제되지 않은
    /// 클라이언트는 남은 시간을 0으로 읽어서, 밤이 시작하자마자 경보가 울린다.
    public bool ReturnAlarm =>
        !Finished && Current == Phase.Night && Remaining > 0f && Remaining <= returnAlarmSeconds;

    /// 순수 전이 규칙. 순서를 한 줄로 읽을 수 있도록 분리해 뒀다.
    /// 페이즈가 시작되는 순간 서버에서 발생한다. 경계에 반응해야 하는 시스템은 각자
    /// 엣지를 폴링하지 말고 여기를 구독한다. 같은 전이를 두 컴포넌트가 따로 감시하다가
    /// 안개 초기화가 조용히 멈춘 적이 있다.
    ///
    /// static이 아니라 인스턴스 이벤트다. static 이벤트는 도메인 리로드나 종료된 매치가
    /// 이미 파괴한 오브젝트의 핸들러를 붙들고 있다가 디스폰된 NetworkBehaviour로 발생시킨다
    /// (아키텍처_v1.0.md §1.5). 구독자는 스폰 때 한 번 해석한 MatchDirector.Phase로 접근한다.
    public event System.Action<Phase> PhaseEntered;

    public static Phase NextPhase(Phase p) => p switch
    {
        Phase.Night => Phase.Transition,
        Phase.Transition => Phase.Day,
        _ => Phase.Night,
    };

    /// 개발 치트가 목표로 삼은 날짜. `NoSkipTarget`이면 치트가 걸려 있지 않다.
    /// 서버에만 있는 값이라 복제하지 않는다.
    int skipToDay = NoSkipTarget;
    const int NoSkipTarget = 0;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        day.Value = 1;
        finished.Value = false;   // 다시 스폰된 루프가 영구 종료 상태로 남으면 안 된다
        skipToDay = NoSkipTarget; // 이전 매치의 치트가 새 판까지 따라오면 안 된다
        Enter(Phase.Night);
    }

    /// 개발 치트. 지금 페이즈를 즉시 마감한다. 전이·날짜 증가·종료 판정은 아래 정규 경로가
    /// 그대로 하므로 임대료 정산(전환)과 `PhaseEntered` 구독자가 실제 진행과 똑같이 돈다.
    /// 전이 규칙을 여기서 다시 쓰면 두 벌이 갈라진다.
    public void EndPhaseNowServer()
    {
        if (!IsServer || finished.Value) return;
        endsAt.Value = NetworkManager.ServerTime.Time;
    }

    /// 개발 치트. 날짜가 하나 넘어갈 때까지 페이즈를 연달아 마감한다.
    /// 밤에서 누르면 전환과 낮을 지나 다음 밤까지 간다.
    public void SkipToNextDayServer()
    {
        if (!IsServer || finished.Value) return;
        skipToDay = day.Value + 1;
    }

    void Update()
    {
        if (!IsServer || finished.Value) return;

        // 치트가 걸려 있으면 목표 날짜에 닿을 때까지 매 프레임 마감을 당긴다. 전이는 어디까지나
        // 아래 정규 경로가 한 프레임에 하나씩 처리한다.
        if (skipToDay != NoSkipTarget)
        {
            if (day.Value >= skipToDay) skipToDay = NoSkipTarget;
            else endsAt.Value = NetworkManager.ServerTime.Time;
        }

        if (NetworkManager.ServerTime.Time < endsAt.Value) return;

        // 낮이 끝나면 날짜를 넘긴다. 마지막 날이면 판을 종료한다.
        if (phase.Value == Phase.Day)
        {
            if (day.Value >= totalDays) { finished.Value = true; return; }
            day.Value++;
        }
        Enter(NextPhase(phase.Value));
    }

    void Enter(Phase p)
    {
        phase.Value = p;
        endsAt.Value = NetworkManager.ServerTime.Time + Duration(p);
        PhaseEntered?.Invoke(p);
    }

    float Duration(Phase p) => p switch
    {
        Phase.Night => nightSeconds,
        Phase.Transition => transitionSeconds,
        _ => daySeconds,
    };
}
