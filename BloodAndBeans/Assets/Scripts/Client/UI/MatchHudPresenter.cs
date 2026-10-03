using System.Text;
using Unity.Netcode;
using UnityEngine;

/// 매치 HUD에 무엇을 쓸지 정한다. 복제된 상태를 읽어 칸별 값(`MatchHudModel`)으로 만든다.
///
/// MonoBehaviour가 아니다. 갱신 시점만 바깥(`MatchFlow`)에서 받고, 나머지는 전부
/// 순수 계산이다.
public sealed class MatchHudPresenter
{
    readonly UIMatchHudScreen view;
    readonly GamePhase phase;
    readonly TransitionLedger ledger;
    readonly float refreshInterval;
    readonly StringBuilder text = new();
    readonly System.Collections.Generic.List<MatchHudModel.Standing> standings = new();

    float nextRefresh;

    // 매 갱신마다 다시 찾지 않는다. 늦게 생기는 참조는 아직 없을 때만 한 번 찾는다.
    MatchDirector director;
    NetworkObject cachedPlayer;
    PlayerInventory inventory;

    /// 이동과 상호작용이 한 컴포넌트다 (`PlayerController`) — 예전에는 `PlayerMove`·
    /// `PlayerInteractor` 둘로 나뉘어 이 필드도 둘이었다.
    PlayerController controller;
    DashHarass dash;
    PlayerInputRouter input;
    PlayerCarry carry;
    PlayerCharacter character;
    PlayerAbilities abilities;

    /// 같은 팀 다른 사람의 손. 낮의 조작은 "재료를 옮기는 것"이 전부라(기획서 5.1)
    /// 팀원이 무엇을 들었는지가 곧 다음에 무엇을 할지다.
    ///
    /// 늦게 생기므로 아직 못 잡았을 때만 찾고, 잡은 뒤에는 다시 찾지 않는다 (AGENTS.md).
    /// 1인 1팀이면 영영 못 찾지만, 후보가 접속자 수(최대 8)뿐이라 갱신 주기당 그 순회가
    /// 전부다.
    PlayerCarry mate;

    /// 손 아이템이 놓이는 자리를 든 표현 컴포넌트. 제조 카드가 그 위에 뜬다 (5.7.3).
    PlayerVisuals visuals;
    PlayerVisuals mateVisuals;

    /// 브레인이 붙은 카메라. 귀환 방향을 화면 기준으로 돌리는 데만 쓴다. 늦게 생기므로
    /// 아직 못 잡았을 때만 한 번 찾고, 잡은 뒤에는 다시 찾지 않는다 (AGENTS.md).
    Camera cam;

    /// 로컬 플레이어의 상호작용 컴포넌트. 여기서 이미 한 번 풀어 두므로 루팅 창을
    /// 여닫는 `MatchFlow`가 같은 것을 다시 찾지 않는다. 상자 홀드(`LootBox`,
    /// `TakeSlotClient`, `CastProgress01`)도 이 컴포넌트 하나가 갖는다.
    public PlayerController Interactor => controller;

    /// 로컬 플레이어의 가방. 여기서 이미 한 번 풀어 두므로 루팅 창이 같은 것을 다시
    /// `GetComponent`로 찾지 않는다.
    public PlayerInventory Bag => inventory;

    public MatchHudPresenter(UIMatchHudScreen view, GamePhase phase,
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
        var cafe = director != null ? director.CafeOf(PlayerTeam.Local()) : null;
        view.RenderGems(model.IsDay ? cafe : null);
        if (model.IsDay && view.RecipeOpen)
            view.RenderRecipes(cafe != null ? cafe.Stock : null, ledger != null ? ledger.PopularShown : null);
    }

    MatchHudModel BuildModel()
    {
        var model = new MatchHudModel();
        if (phase == null || !phase.IsSpawned) return model;

        RefreshLocalPlayer();
        var team = PlayerTeam.Local();

        model.Day = $"{phase.Day}일차";
        model.PhaseName = phase.Finished ? "종료" : PhaseLabel(phase.Current);
        model.Timer = phase.Finished ? "--:--" : Clock(phase.Remaining, phase.Current == Phase.Day);
        model.Team = DisplayNames.Team(team);

        // 호스트는 자기 자신이 서버라 잴 왕복이 없다.
        var net = phase.NetworkManager;
        model.Ping = net.IsServer
            ? "호스트"
            : $"핑 {net.NetworkConfig.NetworkTransport.GetCurrentRtt(NetworkManager.ServerClientId)}ms";

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

                // 게이지 색이 구간마다 바뀐다 (기획서 6.7). 표는 `LoadBands`에 있고
                // 화면은 인덱스만 받는다 — 어느 색인지는 표현의 몫이다.
                model.BagBand = LoadBands.BandOf(inventory.LoadRatio);
                model.BagPercent = $"가방 용량  {inventory.LoadRatio * 100f:0}%"
                    + $"   속도 {(controller != null ? controller.SpeedScale : 1f) * 100f:0}%";
                model.BagWeight = $"{inventory.Carried:0.0} / {inventory.Capacity:0.0} KG";
            }
            else
            {
                // 묻힌 동안에는 적재량이 의미가 없다. 게이지를 비우고 화면이 색으로 알린다.
                model.BagBuried = true;
                model.BagRatio = 0f;
                model.BagBand = 0;
                model.BagPercent = "가방 없음";
                model.BagWeight = "귀환 지점에서 빈 가방 재지급";
            }
        }

        // 대시는 밤과 낮 모두 쓴다 (기획서 11장 조작 표). 전환은 조작을 받지 않는다.
        if (phase.Current != Phase.Transition) FillDash(ref model);

        model.IsDay = phase.Current == Phase.Day && !phase.Finished;
        model.DayCounter = $"{phase.Day}/{DayPhases.TotalDays}";
        model.DayRemaining = phase.Remaining;
        var dayDuration = phase.Duration(Phase.Day);
        model.DayTimeRatio = dayDuration > 0f ? Mathf.Clamp01(model.DayRemaining / dayDuration) : 0f;
        // 팀원의 제조 카드도 그린다. 낮에는 `BuildDetails`가 돌지 않아 여기서 찾는다 — 찾은 뒤에는 다시 찾지 않는다.
        if (model.IsDay) RefreshMate(team);
        if (model.IsDay && cafe != null)
        {
            model.Revenue = $"오늘 매출 {cafe.DaySales:N0} / {cafe.DayBill:N0}G";
            model.RentMet = cafe.DayBill > 0 && cafe.DaySales >= cafe.DayBill;
            model.DaySales = cafe.DaySales;
            model.DayBill = cafe.DayBill;
            model.ShowDash = false;
            standings.Clear();
            if (board != null)
                foreach (var rankedTeam in board.Ranking())
                    standings.Add(new MatchHudModel.Standing
                    {
                        Team = rankedTeam,
                        Name = DisplayNames.Team(rankedTeam),
                        Revenue = board.RevenueOf(rankedTeam),
                        Mine = rankedTeam == team,
                    });
            model.Standings = standings;
        }
        model.Details = model.IsDay ? string.Empty : BuildDetails(team, cafe, board);

        // `controller.Prompt`를 두 번 읽지 않는다. 한 번만 읽어 한 갱신 안에서 두 번
        // 훑던 것을 없앤다 — `PlayerController`가 프레임당 한 번만 대상을 캐시하므로
        // 값 자체는 같지만, 여기서 두 번 읽으면 그 절약이 무의미해진다.
        var prompt = controller != null ? controller.Prompt : string.Empty;

        // 건네기는 중앙이 아니라 양쪽 머리 위에 뜬다 (기획서 5.7.4) — `Handoff`가 그린다.
        if (controller != null && controller.Target is PlayerCarry) prompt = string.Empty;

        model.Prompt = !string.IsNullOrEmpty(prompt) ? $"[F] {prompt}"
            : controller != null && controller.Denied ? "안 됨"
            : null;
        return model;
    }

    string BuildDetails(int team, Cafe cafe, Scoreboard board)
    {
        text.Clear();
        if (character == null && cachedPlayer != null) character = cachedPlayer.GetComponent<PlayerCharacter>();
        if (abilities == null && cachedPlayer != null) abilities = cachedPlayer.GetComponent<PlayerAbilities>();
        if (character != null && character.HasPick && phase.Current != Phase.Transition)
        {
            var skillName = phase.Current == Phase.Day ? character.Def.DayName : character.Def.NightName;
            var left = abilities != null ? abilities.CooldownRemaining : 0f;
            text.AppendLine($"[1] {skillName} · {left:0.0}s");
        }

        if (phase.Current == Phase.Transition && ledger != null)
        {
            text.AppendLine("내일의 손님");
            for (var race = 0; race < ledger.RaceCounts.Length; race++)
                if (ledger.RaceCounts[race] > 0) text.AppendLine($"{DisplayNames.Of((Race)race)} x{ledger.RaceCounts[race]}");
            text.AppendLine($"인기 재료: {string.Join(", ", ledger.PopularShown)}");
        }
        else if (phase.Current == Phase.Day && board != null)
        {
            var ranking = board.Ranking();
            for (var rank = 0; rank < ranking.Count; rank++)
            {
                var rankedTeam = ranking[rank];
                text.AppendLine($"{rank + 1}. {DisplayNames.Team(rankedTeam)} · {board.RevenueOf(rankedTeam)}g" +
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

        // 식기 개수는 패널로 세지 않는다 (기획서 5.7.1). 선반과 쌓인 더미를 보면 끝난다 (5.7.3).
        if (cafe?.Queue != null)
            foreach (var customer in cafe.Queue.Waiting)
                if (customer != null)
                    // 인내심은 손님 머리 위 게이지가 보여 준다 (`UICustomerOrder` 말풍선 채움).
                    text.AppendLine($"{DisplayNames.Of(customer.Kind)} · x{customer.Remaining}");

        return text.ToString();
    }

    /// 대시 칸. 못 쓴다면 이유가 무게인지 쿨다운인지까지 적는다 — 이유가 없으면 대시가
    /// 고장 난 것처럼 보인다.
    void FillDash(ref MatchHudModel model)
    {
        if (dash == null || abilities == null) return;

        model.ShowDash = true;
        model.DashKey = input != null ? input.DashBinding : string.Empty;
        if (dash.BlockedByLoad)
        {
            model.DashTime = "과적";
            model.DashRatio = 1f;
            return;
        }

        // 쿨다운은 공통 슬롯이 든다. 무게 차단만 몸(`DashHarass`)이 답한다.
        var left = abilities.CommonCooldownRemaining;
        var full = abilities.CommonCooldownDuration;
        model.DashTime = left > 0f ? Mathf.CeilToInt(left).ToString() : string.Empty;
        model.DashRatio = left > 0f && full > 0f ? Mathf.Clamp01(left / full) : 0f;
    }

    /// 귀환 지시기 한 프레임분. 화면 어디에 놓을지와 무엇을 쓸지만 담는다 —
    /// 화면 좌표로 옮기는 것은 캔버스 크기를 아는 `UIMatchHudScreen`의 일이다.
    public struct ReturnMarker
    {
        public bool Show;
        public Vector2 Viewport;   // 0~1. 화면 밖이면 그 범위를 벗어난 값이 그대로 온다
        public bool Offscreen;
        public float Angle;        // 화살표 회전(도). 화면 밖일 때만 의미가 있다
        public string Label;       // "귀환 · 42m"
    }

    /// 건네기 프롬프트 한 프레임분. 포커스 대상이 팀원일 때만 켜진다 (기획서 5.7.4).
    public struct HandoffMarker
    {
        public bool Show;
        public Vector3 Self;       // 두 사람의 발밑 좌표. 머리 위 높이는 화면이 정한다
        public Vector3 Mate;
        public string Label;       // "[F] 건네기"
        public Camera View;
    }

    /// 제조 카드 한 프레임분 (기획서 5.7.3). 나와 팀원의 손. 공개 범위가 자기 팀이라 둘뿐이다.
    public struct MakingCards
    {
        public bool ShowSelf;
        public CarryView Self;
        public Vector3 SelfAt;     // 들고 있는 식기 자리. 카드 높이는 화면이 정한다
        public bool ShowMate;
        public CarryView Mate;
        public Vector3 MateAt;
        public Camera View;
    }

    /// 매 프레임 불린다 — 식기를 따라다녀야 한다. 문자열은 만들지 않는다.
    public MakingCards Cards
    {
        get
        {
            var cards = new MakingCards();
            if (phase == null || !phase.IsSpawned || phase.Current != Phase.Day || phase.Finished) return cards;

            if (cam == null) cam = Camera.main;
            if (cam == null) return cards;
            cards.View = cam;

            if (carry != null && visuals != null)
            {
                cards.Self = carry.View;
                cards.ShowSelf = cards.Self.PartCount > 0;
                cards.SelfAt = visuals.HeldAnchor.position;
            }
            if (mate != null && mateVisuals != null)
            {
                cards.Mate = mate.View;
                cards.ShowMate = cards.Mate.PartCount > 0;
                cards.MateAt = mateVisuals.HeldAnchor.position;
            }
            return cards;
        }
    }

    string handoffPrompt;
    string handoffLabel;

    /// 매 프레임 불린다 — 두 사람이 움직이는 동안 머리를 따라가야 한다. 문구는 바뀔 때만 만든다.
    public HandoffMarker Handoff
    {
        get
        {
            var marker = new HandoffMarker();
            if (controller == null || cachedPlayer == null || controller.Target is not PlayerCarry mate) return marker;

            if (cam == null) cam = Camera.main;
            if (cam == null) return marker;

            var prompt = controller.Prompt;
            if (!ReferenceEquals(prompt, handoffPrompt))
            {
                handoffPrompt = prompt;
                handoffLabel = $"[F] {prompt}";
            }

            marker.Show = true;
            marker.Self = cachedPlayer.transform.position;
            marker.Mate = mate.transform.position;
            marker.Label = handoffLabel;
            marker.View = cam;
            return marker;
        }
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

    /// 낮 시계는 m:ss, 밤은 초 단위로 쫓기는 구간이라 mm:ss.fff로 표시한다.
    static string Clock(float seconds, bool compact)
    {
        var span = System.TimeSpan.FromSeconds(Mathf.Max(0f, seconds));
        return compact ? $"{(int)span.TotalMinutes}:{span.Seconds:00}"
            : $"{(int)span.TotalMinutes:00}:{span.Seconds:00}.{span.Milliseconds:000}";
    }

    static string PhaseLabel(Phase p) => p switch
    {
        Phase.Night => "야간 탐색",
        Phase.Transition => "전환",
        _ => "주간 영업",
    };

    /// 개봉 게이지 진행도(0~1). 화면 가운데 막대로 그리는 것은 `UIMatchHudScreen`의 일이고,
    /// 여기는 값만 넘긴다. HUD 글자 덩어리에 섞으면 오른쪽 열에 붙어 시선에서 벗어난다.
    public float CastProgress01
    {
        get
        {
            // 묻은 가방 회수·소각도 같은 막대를 쓴다. 다 파내면 서버가 디스폰하므로 파괴 판정을 거친다.
            if (controller != null && controller.Current is BuriedBag bag && bag != null)
                return bag.CastProgress01;
            return controller != null ? controller.CastProgress01 : 0f;
        }
    }


    /// 완성 게이지 한 프레임분 (기획서 5.2). 침 위치와 판정 구간만 담는다 — 화면 어디에
    /// 얼마만 한 막대로 그릴지는 `UIMatchHudScreen`이 정한다.
    public struct GaugeView
    {
        public bool Show;
        public float Needle;        // 0~1. 0.5가 중앙
        public float PerfectHalf;   // 중앙 0.5로부터의 반폭
        public float GoodHalf;
        public string Label;        // "Oven · 6.3s"
    }

    string gaugeLabel;
    int gaugeLabelTenths = -1;
    CompletionGauge gaugeLabelOwner;

    /// 매 프레임 불린다. 침은 초당 1.4회 왕복이라 HUD 갱신 주기(0.1초)로 그리면
    /// 계단이 되고 판정 구간을 눈으로 노릴 수 없다 — 개봉 게이지와 같은 이유다.
    ///
    /// F는 설비를 조준하지 않고 *자기 카페에서 가장 오래된* 게이지를 멈춘다 (기획서 5.2).
    /// 그래서 여기 뜨는 것도 설비 옆에 서 있는지와 무관하게 바로 그 게이지 하나다.
    public GaugeView Gauge
    {
        get
        {
            var view = new GaugeView();
            if (phase == null || !phase.IsSpawned || phase.Current != Phase.Day) return view;

            var gauge = CompletionGauge.LocalTarget();
            if (gauge == null)
            {
                gaugeLabelOwner = null;
                return view;
            }

            view.Show = true;
            view.Needle = gauge.Needle;
            view.PerfectHalf = gauge.PerfectHalfWidth;
            view.GoodHalf = gauge.GoodHalfWidth;

            // 문자열은 표시할 0.1초가 바뀔 때만 다시 만든다. 매 프레임 만들면 그대로 GC다
            // (귀환 마커의 거리 라벨과 같은 방식).
            var tenths = Mathf.CeilToInt(gauge.Remaining * 10f);
            if (tenths != gaugeLabelTenths || !ReferenceEquals(gauge, gaugeLabelOwner))
            {
                gaugeLabelTenths = tenths;
                gaugeLabelOwner = gauge;
                gaugeLabel = $"{gauge.StationName} · {tenths * 0.1f:0.0}s";
            }
            view.Label = gaugeLabel;
            return view;
        }
    }


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
        controller = player != null ? player.GetComponent<PlayerController>() : null;
        dash = player != null ? player.GetComponent<DashHarass>() : null;
        input = player != null ? player.GetComponent<PlayerInputRouter>() : null;
        abilities = player != null ? player.GetComponent<PlayerAbilities>() : null;
        carry = player != null ? player.GetComponent<PlayerCarry>() : null;
        visuals = player != null ? player.GetComponent<PlayerVisuals>() : null;

        // 로컬 플레이어가 바뀌면 팀도 바뀔 수 있다. 옛 팀의 팀원을 계속 들고 있으면
        // 남의 손을 내 HUD에 그린다.
        mate = null;
        mateVisuals = null;
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
            mateVisuals = player.GetComponent<PlayerVisuals>();
            if (mate != null) return;
        }
    }
}


