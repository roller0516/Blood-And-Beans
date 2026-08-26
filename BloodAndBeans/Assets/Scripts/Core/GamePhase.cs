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

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        day.Value = 1;
        finished.Value = false;   // 다시 스폰된 루프가 영구 종료 상태로 남으면 안 된다
        Enter(Phase.Night);
    }

    void Update()
    {
        if (!IsServer || finished.Value) return;
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
