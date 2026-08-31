using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

/// 루프의 장부를 마감한다. 기획서 2장의 순서는 밤 -> 전환 -> 낮이므로 낮에서 전환으로
/// 가는 경계는 없다. 임대료는 낮이 끝나고 밤으로 넘어갈 때 청구되고 (3.2), 예보는 밤이
/// 끝날 때 뽑아서 전환 화면이 곧 시작될 낮에 대해 보여 줄 것을 갖게 한다 (5.6).
public class TransitionLedger : NetworkBehaviour
{
    [SerializeField] int ordersPerDay = 8;

    readonly List<int> revenueAtDayStart = new();

    Phase last = Phase.Night;

    /// GamePhase는 낮을 빠져나가기 전에 날짜 카운터를 올린다. 그래서 여기가 경계를 볼
    /// 때는 달력이 이미 내일을 가리킨다. 다시 읽어 오는 대신 마감 중인 날짜를 여기서
    /// 따로 추적한다 (기획서 3.2: "그날의 임대료").
    int dayClosing = 1;
    MatchDirector director;
    GamePhase phase;

    /// 마지막 날을 이미 마감했는가. 마지막 낮은 페이즈가 바뀌지 않고 끝나므로
    /// (`GamePhase`가 `finished`만 세우고 멈춘다) 경계 감지로는 잡히지 않는다.
    bool closedFinalDay;

    /// 판 하나짜리 매출판. 예전에는 `FindFirstObjectByType`으로 아무거나 하나를 집었는데,
    /// 매출판이 카페마다 있던 탓에 한 팀의 장부만 읽거나 아예 null로 굳어 임대료가
    /// 매일 전액 미납이 됐다.
    Scoreboard Board => director != null ? director.Board : null;

    public Forecast Tomorrow { get; private set; }
    public int TeamCount => director != null ? director.TeamCount : 0;

    /// 예보 패널이 보여 주는 값. 예보 자체는 서버 전용이고, 클라이언트는 예보가 뽑히는
    /// 순간 이 두 요약만 받는다 (5.6.3: 손님 구성과 태그뿐, 어느 박스에 무엇이 들었는지는
    /// 알려 주지 않는다).
    public int[] RaceCounts { get; private set; } = new int[6];
    public Ingredient[] PopularShown { get; private set; } = System.Array.Empty<Ingredient>();

    /// 임대료 장부 자체는 이 컴포넌트가 아니라 팀이 소유한다. 여기서는 마감만 한다.
    public Rent RentOf(int team) => director?.LedgerOf(team)?.Rent;

    /// 전환 화면이 보여 줄 하루 마감 결과. 장부는 서버에만 있으므로 마감 순간에 자기 팀
    /// 클라이언트로만 보낸다 — 남의 임대료와 부채는 공개 대상이 아니다 (기획서 3.1).
    public readonly struct Settlement
    {
        public readonly int Day;
        public readonly int Sales;
        public readonly int RentOwed;
        public readonly int RentPaid;
        public readonly int Debt;
        public readonly int MissStreak;
        public readonly bool Valid;

        public Settlement(int day, int sales, int owed, int paid, int debt, int missStreak)
        {
            Day = day; Sales = sales; RentOwed = owed; RentPaid = paid;
            Debt = debt; MissStreak = missStreak; Valid = true;
        }
    }

    /// 마지막으로 받은 마감 결과. 아직 하루도 마감되지 않았으면 `Valid`가 false다.
    public Settlement Today { get; private set; }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        director = MatchDirector.Instance;
        phase = director != null ? director.Phase : null;
        closedFinalDay = false;

        // 다시 스폰됐을 때 두 번째 세트가 덧붙으면 안 된다. 그러면 CloseDay가 Scoreboard
        // 범위를 넘겨 인덱싱한다.
        revenueAtDayStart.Clear();
        dayClosing = 1;

        // Penalties.ResetServer()는 더 이상 없다. 장부는 MatchDirector.Awake가 매번 새로
        // 만들므로 비울 static이 남아 있지 않다.
        for (int i = 0; i < TeamCount; i++) revenueAtDayStart.Add(0);
    }

    void Update()
    {
        if (!IsServer || phase == null) return;

        // 낮 종료 -> 임대료 청구 (3.2).
        if (last == Phase.Day && phase.Current != Phase.Day) CloseDay();

        // 마지막 낮은 페이즈가 바뀌지 않는다. `GamePhase.Update`가 `finished`만 세우고
        // 멈추므로 위의 경계 감지가 영영 걸리지 않아 그날 임대료가 공짜였다.
        else if (!closedFinalDay && phase.Finished && last == Phase.Day)
        {
            closedFinalDay = true;
            CloseDay();
        }
        // 밤 종료 -> 전환 화면이 보여 줄 내용을 뽑는다 (5.6).
        if (last == Phase.Night && phase.Current == Phase.Transition) DrawForecast();
        last = phase.Current;
    }

    void CloseDay()
    {
        for (int team = 0; team < revenueAtDayStart.Count; team++)
        {
            var ledger = director.LedgerOf(team);
            if (ledger == null) continue;

            // 오늘의 임대료는 오늘 번 것으로만 낸다. 나머지는 지난 기록이다.
            var board = Board;
            var revenue = board != null ? board.RevenueOf(team) : 0;
            var earnedToday = revenue - revenueAtDayStart[team];

            // 청구액은 오늘 임대료에 지난 부채를 더한 것이다 (3.2). Settle이 부채를
            // 갱신하므로 그 전에 읽어야 한다.
            var owed = Rent.Due(dayClosing) + ledger.Rent.Debt;
            var paid = ledger.Rent.Settle(dayClosing, earnedToday);
            ledger.ApplySettledPenalty();
            revenueAtDayStart[team] = revenue;

            SendSettlement(team, new Settlement(
                dayClosing, earnedToday, owed, paid, ledger.Rent.Debt, ledger.Rent.MissStreak));
        }
        dayClosing++;

        ApplyDayPenalties();
    }

    /// 각 팀은 자기 미납에 대해서만 벌을 받는다. 팀 0의 단계를 모든 카페에 적용하던 탓에
    /// 한 팀의 빚이 다른 팀의 그릇을 깨뜨렸다.
    void ApplyDayPenalties()
    {
        for (var team = 0; team < TeamCount; team++)
        {
            var cafe = director.CafeOf(team);
            var ledger = director.LedgerOf(team);
            if (cafe == null || ledger == null) continue;

            var machines = cafe.GetComponentsInChildren<CoffeeMachine>(true);
            for (var i = 0; i < machines.Length; i++)
                machines[i].SetDisabledServer(ledger.MachineDown && i == machines.Length - 1);

            if (cafe.Dishes != null) cafe.Dishes.SetBreakageServer(ledger.BreaksDish);
        }
    }

    void DrawForecast()
    {
        var seed = Random.Range(int.MinValue, int.MaxValue);
        var menus = Menus.All.Select(m => (IReadOnlyList<Ingredient>)m.Parts).ToList();
        var forecasts = new Forecast[TeamCount];
        for (var team = 0; team < TeamCount; team++)
        {
            forecasts[team] = Forecast.Build(seed, RegenPool(), menus, HeldByTeam(team), ordersPerDay);
            director.CafeOf(team)?.Queue?.SetDayPlanServer(forecasts[team]);
        }
        Tomorrow = forecasts.Length > 0 ? forecasts[0] : null;
        if (Tomorrow == null) return;

        // 계산대가 오늘의 인기 재료 목록을 알아야만 보너스가 가격에 반영된다 (5.6.1).
        foreach (var till in FindObjectsByType<SaleRegister>(FindObjectsSortMode.None))
            till.Popular = Tomorrow.Popular;

        for (var team = 0; team < forecasts.Length; team++)
        {
            var clients = ClientsOfTeam(team);
            if (clients.Count == 0) continue;
            var forecast = forecasts[team];
            var popular = System.Array.ConvertAll(forecast.Popular, i => (int)i);
            foreach (var clientId in clients)
                ForecastRpc(forecast.RaceCounts, popular,
                    RpcTarget.Single(clientId, RpcTargetUse.Temp));
        }
    }

    /// 자기 팀 클라이언트에게만 보낸다. 예보와 같은 방식이며 이유도 같다 — 임대료·부채는
    /// 팀 바깥에 공개되지 않는다 (기획서 3.1).
    void SendSettlement(int team, Settlement s)
    {
        var clients = ClientsOfTeam(team);
        if (clients.Count == 0) return;

        foreach (var clientId in clients)
            SettlementRpc(s.Day, s.Sales, s.RentOwed, s.RentPaid, s.Debt, s.MissStreak,
                          RpcTarget.Single(clientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    void SettlementRpc(int day, int sales, int owed, int paid, int debt, int missStreak,
                       RpcParams p = default)
    {
        Today = new Settlement(day, sales, owed, paid, debt, missStreak);
    }

    /// 보내고 잊는다. 패널은 10초짜리 전환 동안만 살아 있으므로, 전환 도중에 접속한
    /// 클라이언트는 그냥 다음 예보를 보게 된다.
    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    void ForecastRpc(int[] raceCounts, int[] popular, RpcParams p = default)
    {
        RaceCounts = raceCounts;
        PopularShown = System.Array.ConvertAll(popular, i => (Ingredient)i);
    }

    /// 오늘 밤 숲이 내놓는 것. 카페 상비 재료는 여기 없다. Forecast가 제작 가능 판정을
    /// 위해 따로 더해 주고 인기 재료 추첨에서는 제외한다 (기획서 7.1, 5.6.1).
    /// ponytail: DT_Regen(기획서 10장)이 생기기 전까지는 모든 숲 재료가 리젠된다.
    static IReadOnlyList<Ingredient> RegenPool() => new[]
    {
        Ingredient.Milk, Ingredient.Cream, Ingredient.Chocolate,
        Ingredient.Almond, Ingredient.Berry, Ingredient.Ice,
    };

    /// 주문 구성의 30% 몫은 팀이 실제로 보유한 것을 본다 (기획서 5.5 규칙 3).
    /// 이제 밤의 수확이 재고로 들어가므로 실제 재고를 읽는다. ReturnZone은 페이즈 이벤트에서
    /// 정산하고 그 이벤트는 이 Update가 새 페이즈를 보기 전에 발생하므로, 예보를 뽑는 시점에는
    /// 모든 입고가 끝나 있다.
    /// 첫날 밤에는 비어 있고, 그 경우 Forecast가 리젠 풀로 대체한다.
    IReadOnlyList<Ingredient> HeldByTeam(int team)
    {
        var held = new List<Ingredient>();
        director.CafeOf(team)?.Stock?.CopyHeldTo(held);
        return held;
    }

    List<ulong> ClientsOfTeam(int team)
    {
        var clients = new List<ulong>();
        foreach (var client in NetworkManager.ConnectedClientsList)
            if (PlayerTeam.Of(client.ClientId) == team) clients.Add(client.ClientId);
        return clients;
    }
}
