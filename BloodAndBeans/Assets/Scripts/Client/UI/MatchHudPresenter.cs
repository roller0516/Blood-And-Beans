using System.Text;
using Unity.Netcode;
using UnityEngine;

/// 매치 HUD에 무엇을 쓸지 정한다. 복제된 상태를 읽어 칸별 값(`MatchHudModel`)으로 만든다.
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
    PlayerCarry carry;

    /// 같은 팀 다른 사람의 손. 낮의 조작은 "재료를 옮기는 것"이 전부라(기획서 5.1)
    /// 팀원이 무엇을 들었는지가 곧 다음에 무엇을 할지다.
    ///
    /// 늦게 생기므로 아직 못 잡았을 때만 찾고, 잡은 뒤에는 다시 찾지 않는다 (AGENTS.md).
    /// 1인 1팀이면 영영 못 찾지만, 후보가 접속자 수(최대 8)뿐이라 갱신 주기당 그 순회가
    /// 전부다.
    PlayerCarry mate;

    /// 브레인이 붙은 카메라. 귀환 방향을 화면 기준으로 돌리는 데만 쓴다. 늦게 생기므로
    /// 아직 못 잡았을 때만 한 번 찾고, 잡은 뒤에는 다시 찾지 않는다 (AGENTS.md).
    Camera cam;

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

    /// 매 프레임 불러도 된다. 값은 `refreshInterval`마다 한 번만 만든다.
    public void Tick(float unscaledTime)
    {
        if (view == null || unscaledTime < nextRefresh) return;
        nextRefresh = unscaledTime + refreshInterval;

        var model = BuildModel();
        view.Render(model);
    }

    MatchHudModel BuildModel()
    {
        var model = new MatchHudModel();
        if (phase == null || !phase.IsSpawned) return model;

        RefreshLocalPlayer();
        var team = PlayerTeam.Local();

        model.Day = $"{phase.Day}일차";
        model.PhaseName = phase.Finished ? "종료" : PhaseLabel(phase.Current);
        model.Timer = phase.Finished ? "--:--" : Clock(phase.Remaining);
        model.Team = TeamLabel(team);

        if (director == null) director = MatchDirector.Instance;
        var cafe = director != null ? director.CafeOf(team) : null;

        // 매출판은 카페 프리팹에 붙어 있다. 씬에서 이을 수 없어 예전 `PhaseHud`의 직렬화
        // 칸은 늘 비어 있었고, 그래서 낮 순위가 한 번도 그려지지 않았다.
        var board = cafe != null ? cafe.Board : null;
        model.Revenue = board != null && team >= 0
            ? $"팀 매출  {board.RevenueOf(team):N0}G"
            : "팀 매출  --";

        if (phase.Current == Phase.Night && inventory != null)
        {
            model.ShowBag = true;
            if (inventory.HasBag)
            {
                model.BagRatio = inventory.LoadRatio;
                model.BagPercent = $"가방 용량  {inventory.LoadRatio * 100f:0}%"
                    + $"   속도 {inventory.CurrentSpeedMultiplier * 100f:0}%";
                model.BagWeight = $"{inventory.Carried:0.0} / {inventory.Capacity:0.0} KG";
            }
            else
            {
                // 묻힌 동안에는 적재량이 의미가 없다. 게이지를 비우고 화면이 색으로 알린다.
                model.BagBuried = true;
                model.BagRatio = 0f;
                model.BagPercent = "가방을 묻어 뒀다";
                model.BagWeight = "회수하지 않으면 수확 전량 소실";
            }

            FillDash(ref model);
        }

        model.Details = BuildDetails(team, cafe, board);
        model.Prompt = interactor != null && !string.IsNullOrEmpty(interactor.Prompt)
            ? $"[F] {interactor.Prompt}"
            : null;
        return model;
    }

    string BuildDetails(int team, Cafe cafe, Scoreboard board)
    {
        text.Clear();

        if (phase.Current == Phase.Transition && ledger != null)
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

        // 낮의 조작은 재료를 옮기는 것이 전부다 (기획서 5.1). 무엇을 들었는지가 안 보이면
        // 둘이 같은 주문을 분업할 수 없다 (2.1).
        if (phase.Current == Phase.Day)
        {
            RefreshMate(team);
            if (carry != null) text.AppendLine($"손 · {carry.View.Label}");
            if (mate != null) text.AppendLine($"팀원 · {mate.View.Label}");
        }

        if (cafe?.Dishes != null)
            text.AppendLine($"접시 · 깨끗 {cafe.Dishes.Clean} / 사용 {cafe.Dishes.InUse} / 더러움 {cafe.Dishes.Dirty}");
        if (cafe?.Queue != null)
            foreach (var customer in cafe.Queue.Waiting)
                if (customer != null)
                    text.AppendLine($"{customer.Kind} · x{customer.Remaining} · 인내 {customer.PatienceRatio * 100f:0}%");

        return text.ToString();
    }

    /// 대시 칸. 못 쓴다면 이유가 무게인지 쿨다운인지까지 적는다 — 이유가 없으면 대시가
    /// 고장 난 것처럼 보인다.
    void FillDash(ref MatchHudModel model)
    {
        if (dash == null) return;

        model.ShowDash = true;
        if (dash.BlockedByLoad)
        {
            model.DashTime = "과적";
            model.DashRatio = 1f;
            return;
        }

        var left = dash.CooldownRemaining;
        model.DashReady = left <= 0f;
        model.DashTime = model.DashReady ? "준비" : $"{left:0.0}s";
        model.DashRatio = model.DashReady || dash.Cooldown <= 0f ? 1f : 1f - left / dash.Cooldown;
    }

    /// 귀환 지시기 한 프레임분. 화면 어디에 놓을지와 무엇을 쓸지만 담는다 —
    /// 화면 좌표로 옮기는 것은 캔버스 크기를 아는 `MatchHudScreen`의 일이다.
    public struct ReturnMarker
    {
        public bool Show;
        public Vector2 Viewport;   // 0~1. 화면 밖이면 그 범위를 벗어난 값이 그대로 온다
        public bool Offscreen;
        public float Angle;        // 화살표 회전(도). 화면 밖일 때만 의미가 있다
        public string Label;       // "귀환 · 42m"
    }

    /// 마커가 뜨는 높이. 복귀 구역은 바닥에 깔려 있어서 그 자리에 그대로 붙이면
    /// 지형에 파묻힌 것처럼 보인다.
    const float MarkerHeight = 2.5f;

    string returnLabel;
    int returnLabelMeters = -1;

    /// 밤 마감 직전의 귀환 지시기 (기획서 6.4: 1:30 경보 + 각자의 귀환 방향 표시).
    ///
    /// 매 프레임 불린다. 마커는 월드의 한 점에 붙어 있어서 HUD 갱신 주기(0.1초)로 옮기면
    /// 카메라가 도는 동안 계단처럼 끊긴다 — 개봉 게이지와 같은 이유다.
    /// 문자열은 표시할 거리(m)가 바뀔 때만 다시 만든다. 매 프레임 만들면 그대로 GC다.
    public ReturnMarker Marker
    {
        get
        {
            var marker = new ReturnMarker();
            if (phase == null || !phase.IsSpawned || !phase.ReturnAlarm) return marker;

            if (director == null) director = MatchDirector.Instance;
            var team = PlayerTeam.Local();
            if (director == null || team < 0 || cachedPlayer == null) return marker;

            if (cam == null) cam = Camera.main;
            if (cam == null) return marker;

            // 귀환 지점은 카페가 아니라 그 팀의 밤 시작 지점이다 (기획서 6.8 "소환 위치").
            // `ReturnZone`의 정산도 같은 함수를 읽는다 — 표시와 판정의 출처가 하나여야 한다.
            var home = director.NightSpawnPosition(team, 0);
            var viewport = cam.WorldToViewportPoint(home + Vector3.up * MarkerHeight);

            // 카메라 뒤에 있으면 뷰포트 좌표가 뒤집혀 나온다. 그대로 쓰면 화살표가 정반대를
            // 가리키고, 마커는 반대쪽 가장자리에 붙는다.
            var behind = viewport.z <= 0f;
            if (behind)
            {
                viewport.x = 1f - viewport.x;
                viewport.y = 1f - viewport.y;
            }

            marker.Show = true;
            marker.Viewport = new Vector2(viewport.x, viewport.y);
            marker.Offscreen = behind
                || viewport.x < 0f || viewport.x > 1f
                || viewport.y < 0f || viewport.y > 1f;

            // 화면 중앙에서 목표로 향하는 각. 뷰포트는 가로세로가 똑같이 0~1로 눌린
            // 좌표라 그대로 재면 각이 틀어진다 — 가로를 종횡비만큼 되돌려서 잰다.
            var dir = new Vector2((viewport.x - 0.5f) * cam.aspect, viewport.y - 0.5f);
            marker.Angle = -Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;

            var meters = Mathf.RoundToInt(
                Vector3.Distance(cachedPlayer.transform.position, home));
            if (meters != returnLabelMeters)
            {
                returnLabelMeters = meters;
                returnLabel = $"귀환 · {meters}m";
            }
            marker.Label = returnLabel;
            return marker;
        }
    }

    /// mm:ss.fff. 밤은 초 단위로 쫓기는 구간이라 소수점이 남아 있어야 한다.
    static string Clock(float seconds)
    {
        var span = System.TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
        return $"{(int)span.TotalMinutes:00}:{span.Seconds:00}.{span.Milliseconds:000}";
    }

    static string PhaseLabel(Phase p) => p switch
    {
        Phase.Night => "야간 탐색",
        Phase.Transition => "전환",
        _ => "주간 영업",
    };

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
        carry = player != null ? player.GetComponent<PlayerCarry>() : null;

        // 로컬 플레이어가 바뀌면 팀도 바뀔 수 있다. 옛 팀의 팀원을 계속 들고 있으면
        // 남의 손을 내 HUD에 그린다.
        mate = null;
    }

    /// 같은 팀의 다른 플레이어를 한 번 찾는다. 이미 잡았거나 팀이 없으면 아무것도 하지 않는다.
    void RefreshMate(int team)
    {
        if (mate != null || team < 0) return;

        var manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsClient) return;

        // 스폰된 플레이어 목록을 본다. `ConnectedClientsList`가 아닌 이유는 그쪽의
        // `NetworkClient.PlayerObject`가 원격 클라이언트에 대해 채워진다는 보장이 없기
        // 때문이다. 팀 번호도 `PlayerTeam.Of`(서버 측 조회) 대신 오브젝트에서 직접 읽는다 —
        // 그 값은 복제되는 NetworkVariable이라 클라이언트에서도 옳다.
        var spawner = manager.SpawnManager;
        if (spawner == null) return;

        var players = spawner.PlayerObjects;
        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (player == null || ReferenceEquals(player, cachedPlayer)) continue;

            var owner = player.GetComponent<PlayerTeam>();
            if (owner == null || owner.Team != team) continue;

            mate = player.GetComponent<PlayerCarry>();
            if (mate != null) return;
        }
    }
}
