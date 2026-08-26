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

    readonly NetworkVariable<bool> active = new();
    readonly NetworkVariable<double> startedAt = new();

    /// 서버 전용. Station이 구독해서 제작 결과를 받는다.
    public System.Action<Judgement> OnResult;

    // ponytail: F 한 번은 *자기 카페의* 살아 있는 게이지 중 가장 오래된 것을 멈춘다.
    // 내 기계 둘이 동시에 끝나면 오래된 것부터 처리된다. 플레이에서 어색하면 설비별
    // 조준을 추가한다.
    //
    // 후보 집합은 이 카페의 게이지뿐이다. 예전에는 모든 카페의 게이지를 담은 static
    // 리스트였고, 그래서 카페 B의 게이지가 더 오래됐을 때 카페 A의 플레이어가 B의 오븐을
    // 판정했다 (아키텍처_v1.0.md §1.2). 지나치게 넓은 목록을 필터링하는 대신 목록 자체를
    // 좁혀서 틀릴 여지를 없앴다.
    Cafe cafe;

    /// 조립 루트는 전역이 아니라 소속 카페에서 받는다. 설비마다 따로 찾으면 카페별로
    /// 다른 답이 나올 여지가 생긴다 (아키텍처_v1.0.md §1.4).
    MatchDirector Director => cafe != null ? cafe.Director : null;

    public bool Active => active.Value;

    public float Needle =>
        Mathf.PingPong((float)(NetworkManager.ServerTime.Time - startedAt.Value) * sweepsPerSecond, 1f);

    public float Remaining =>
        Mathf.Max(0f, window - (float)(NetworkManager.ServerTime.Time - startedAt.Value));

    public void BeginServer()
    {
        if (!IsServer || !IsDay) return;
        active.Value = true;
        startedAt.Value = NetworkManager.ServerTime.Time;
    }

    /// 팀 번호가 아니라 소유 카페를 들고 있는다. MatchDirector는 자기 Awake에서 팀 번호를
    /// 배정하는데 두 Awake 사이의 순서에 기대면 안 된다. 부모를 거슬러 올라가는 방식은
    /// 누가 먼저 실행되든 동작한다.
    void Awake()
    {
        cafe = Cafe.Of(this);
    }

    int TeamId => cafe != null ? cafe.TeamId : -1;
    bool IsDay => Director != null && Director.Phase.Current == Phase.Day;

    void Update()
    {
        if (IsServer && IsDay && active.Value && Remaining <= 0f) Finish(Judgement.Burnt);

    }

    public static bool TryStopLocalClient()
    {
        foreach (var gauge in FindObjectsByType<CompletionGauge>(FindObjectsSortMode.None))
        {
            if (!gauge.IsDay || !gauge.active.Value || gauge.TeamId < 0 || gauge.TeamId != PlayerTeam.Local()) continue;
            if (gauge.OldestInThisCafe() != gauge) continue;
            gauge.StopRpc();
            return true;
        }
        return false;
    }

    CompletionGauge OldestInThisCafe()
    {
        if (cafe == null) return null;

        CompletionGauge best = null;
        foreach (var g in cafe.Gauges)
        {
            if (g == null || !g.active.Value) continue;
            if (best == null || g.startedAt.Value < best.startedAt.Value) best = g;
        }
        return best;
    }

    /// `[Rpc(SendTo.Server)]`는 어떤 클라이언트든 호출할 수 있으므로, 팀 검사는 호출부
    /// Update뿐 아니라 반드시 여기에도 있어야 한다.
    [Rpc(SendTo.Server)]
    public void StopRpc(RpcParams p = default)
    {
        if (!IsDay || !active.Value || TeamId < 0) return;
        if (PlayerTeam.Of(p.Receive.SenderClientId) != TeamId) return;
        Finish(Judge(Needle));
    }

    void Finish(Judgement j)
    {
        active.Value = false;
        OnResult?.Invoke(j);
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
