using System.Text;
using Unity.Netcode;
using UnityEngine;

/// 매치 HUD에 무엇을 쓸지 정한다. 복제된 상태를 읽어 한 덩어리 문자열로 만든다.
///
/// MonoBehaviour가 아니다. 갱신 시점만 바깥(`MatchFlow`)에서 받고, 나머지는 전부
/// 순수 계산이다.
public sealed class MatchHudPresenter
{
    readonly MatchHudScreen view;
    readonly GamePhase phase;
    readonly TransitionLedger ledger;
    readonly float refreshInterval;
    readonly StringBuilder text = new();

    float nextRefresh;

    // 매 갱신마다 다시 찾지 않는다. 늦게 생기는 참조는 아직 없을 때만 한 번 찾는다.
    MatchDirector director;
    NetworkObject cachedPlayer;
    PlayerInventory inventory;
    PlayerInteractor interactor;
    PlayerInteract boxHold;
    DashHarass dash;

    /// 로컬 플레이어의 상호작용 컴포넌트. 여기서 이미 한 번 풀어 두므로 루팅 창을
    /// 여닫는 `MatchFlow`가 같은 것을 다시 찾지 않는다.
    public PlayerInteractor Interactor => interactor;

    /// 로컬 플레이어의 박스 홀드 상태. 고른 칸을 루팅 창에 그리는 데 쓴다.
    public PlayerInteract BoxHold => boxHold;

    public MatchHudPresenter(MatchHudScreen view, GamePhase phase,
                             TransitionLedger ledger, float refreshInterval)
    {
        this.view = view;
        this.phase = phase;
        this.ledger = ledger;
        this.refreshInterval = refreshInterval;
    }

    /// 매 프레임 불러도 된다. 문자열은 `refreshInterval`마다 한 번만 만든다.
    public void Tick(float unscaledTime)
    {
        if (view == null || unscaledTime < nextRefresh) return;
        nextRefresh = unscaledTime + refreshInterval;
        view.Render(BuildText());
    }

    string BuildText()
    {
        if (phase == null || !phase.IsSpawned) return string.Empty;

        text.Clear();
        RefreshLocalPlayer();
        var team = PlayerTeam.Local();

        // 팀 번호를 맨 앞에 둔다. 2인 이상으로 붙여 보면 "지금 이 화면이 몇 번 팀인가"가
        // 가장 먼저 필요한 정보다.
        text.AppendLine(phase.Finished
            ? $"FINISHED · {TeamLabel(team)}"
            : $"Day {phase.Day} · {phase.Current} · {phase.Remaining:0.0}s · {TeamLabel(team)}");

        if (director == null) director = MatchDirector.Instance;
        var cafe = director != null ? director.CafeOf(team) : null;

        // 매출판은 카페 프리팹에 붙어 있다. 씬에서 이을 수 없어 예전 `PhaseHud`의 직렬화
        // 칸은 늘 비어 있었고, 그래서 낮 순위가 한 번도 그려지지 않았다.
        var board = cafe != null ? cafe.Board : null;

        if (phase.Current == Phase.Night && inventory != null)
        {
            text.AppendLine(inventory.HasBag
                ? $"짐 {inventory.LoadRatio * 100f:0}% · 속도 {inventory.CurrentSpeedMultiplier * 100f:0}%"
                    + (dash != null && dash.BlockedByLoad ? " · 대시 불가(과적)" : "")
                : "가방을 묻어 뒀다 · 회수하지 않으면 수확 전량 소실");
        }
        else if (phase.Current == Phase.Transition && ledger != null)
        {
            text.AppendLine("내일의 손님");
            for (var race = 0; race < ledger.RaceCounts.Length; race++)
                if (ledger.RaceCounts[race] > 0) text.AppendLine($"{(Race)race} x{ledger.RaceCounts[race]}");
            text.AppendLine($"인기 재료: {string.Join(", ", ledger.PopularShown)}");
        }
        else if (phase.Current == Phase.Day && board != null)
        {
            var ranking = board.Ranking();
            for (var rank = 0; rank < ranking.Count; rank++)
            {
                var rankedTeam = ranking[rank];
                text.AppendLine($"{rank + 1}. Team {rankedTeam} · {board.RevenueOf(rankedTeam)}g" +
                    (rankedTeam == team ? " <" : ""));
            }
        }

        if (cafe?.Dishes != null)
            text.AppendLine($"접시 · 깨끗 {cafe.Dishes.Clean} / 사용 {cafe.Dishes.InUse} / 더러움 {cafe.Dishes.Dirty}");
        if (cafe?.Queue != null)
            foreach (var customer in cafe.Queue.Waiting)
                if (customer != null)
                    text.AppendLine($"{customer.Kind} · x{customer.Remaining} · 인내 {customer.PatienceRatio * 100f:0}%");

        var prompt = interactor != null ? interactor.Prompt : null;
        if (!string.IsNullOrEmpty(prompt)) text.AppendLine($"\n[F] {prompt}");
        return text.ToString();
    }

    /// 개봉 게이지 진행도(0~1). 화면 가운데 막대로 그리는 것은 `MatchHudScreen`의 일이고,
    /// 여기는 값만 넘긴다. HUD 글자 덩어리에 섞으면 오른쪽 열에 붙어 시선에서 벗어난다.
    public float CastProgress01 => boxHold != null ? boxHold.CastProgress01 : 0f;

    static string TeamLabel(int team) => team < 0 ? "팀 배정 대기" : $"Team {team}";

    /// 로컬 플레이어가 바뀔 때만 컴포넌트를 다시 푼다. 갱신마다 `GetComponent`를 부르면
    /// 주기 실행 안의 컴포넌트 조회가 된다 (AGENTS.md 참조와 결합도).
    void RefreshLocalPlayer()
    {
        var manager = NetworkManager.Singleton;
        var player = manager != null && manager.IsClient && manager.LocalClient != null
            ? manager.LocalClient.PlayerObject
            : null;

        if (ReferenceEquals(player, cachedPlayer)) return;

        cachedPlayer = player;
        inventory = player != null ? player.GetComponent<PlayerInventory>() : null;
        interactor = player != null ? player.GetComponent<PlayerInteractor>() : null;
        boxHold = player != null ? player.GetComponent<PlayerInteract>() : null;
        dash = player != null ? player.GetComponent<DashHarass>() : null;
    }
}
