using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;
using UnityEngine;

/// Closes the books on the loop. The order in doc 2장 is Night -> Transition -> Day,
/// so there is no Day->Transition edge: rent falls due when the day ends and hands over
/// to night (3.2), and the forecast is drawn as the night ends so the transition screen
/// has something to show for the day about to start (5.6).
public class TransitionLedger : NetworkBehaviour
{
    [SerializeField] int ordersPerDay = 8;

    readonly List<int> revenueAtDayStart = new();

    Phase last = Phase.Night;

    /// GamePhase bumps its day counter before it leaves Day, so by the time this
    /// sees the edge the calendar already reads tomorrow. Track the day being
    /// closed here instead of reading it back (doc 3.2: "그날의 임대료").
    int dayClosing = 1;
    MatchDirector director;
    GamePhase phase;
    Scoreboard board;

    public Forecast Tomorrow { get; private set; }
    public int TeamCount => director != null ? director.TeamCount : 0;

    /// What the forecast panel shows. The forecast itself is server-only — clients get
    /// these two summaries the moment it is drawn (5.6.3: the mix and the tags, nothing
    /// about which box holds what).
    public int[] RaceCounts { get; private set; } = new int[6];
    public Ingredient[] PopularShown { get; private set; } = System.Array.Empty<Ingredient>();

    /// The rent book itself belongs to the team, not to this component. This closes it.
    public Rent RentOf(int team) => director?.LedgerOf(team)?.Rent;

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        director = MatchDirector.Find();
        phase = director != null ? director.Phase : null;
        board = FindFirstObjectByType<Scoreboard>();

        // Respawn must not append a second set — CloseDay then indexes past Scoreboard.
        revenueAtDayStart.Clear();
        dayClosing = 1;

        // No Penalties.ResetServer() any more: the ledgers are built fresh by
        // MatchDirector.Awake, so there is no static left to clear.
        for (int i = 0; i < TeamCount; i++) revenueAtDayStart.Add(0);
    }

    void Update()
    {
        if (!IsServer || phase == null) return;

        // End of day -> rent is due (3.2).
        if (last == Phase.Day && phase.Current != Phase.Day) CloseDay();
        // Night is over -> draw what the transition screen will show (5.6).
        if (last == Phase.Night && phase.Current == Phase.Transition) DrawForecast();
        last = phase.Current;
    }

    void CloseDay()
    {
        for (int team = 0; team < revenueAtDayStart.Count; team++)
        {
            var ledger = director.LedgerOf(team);
            if (ledger == null) continue;

            // Only what was earned today pays today's rent; the rest is history.
            var earnedToday = (board != null ? board.RevenueOf(team) : 0) - revenueAtDayStart[team];
            ledger.Rent.Settle(dayClosing, earnedToday);
            ledger.ApplySettledPenalty();
            revenueAtDayStart[team] = board != null ? board.RevenueOf(team) : 0;
        }
        dayClosing++;

        ApplyDayPenalties();
    }

    /// Each team is punished for its own misses. Applying team 0's tier to every cafe
    /// meant one team's debt broke the other team's dishes.
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

        // The bonus only reaches a price if the till knows today's popular list (5.6.1).
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

    /// Fire-and-forget: the panel only lives for the 10s transition, so a client that
    /// joins mid-transition simply sees the next one.
    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    void ForecastRpc(int[] raceCounts, int[] popular, RpcParams p = default)
    {
        RaceCounts = raceCounts;
        PopularShown = System.Array.ConvertAll(popular, i => (Ingredient)i);
    }

    /// What the forest offers tonight. Cafe staples are not listed here — Forecast adds
    /// them for craftability and keeps them out of the popular draw (doc 7.1, 5.6.1).
    /// ponytail: every forest ingredient regens until DT_Regen (doc 10장) exists.
    static IReadOnlyList<Ingredient> RegenPool() => new[]
    {
        Ingredient.Milk, Ingredient.Cream, Ingredient.Chocolate,
        Ingredient.Almond, Ingredient.Berry, Ingredient.Ice,
    };

    /// The 30% slice of the order mix looks at what teams actually hold (doc 5.5 rule 3).
    /// Reads the real larders now that the night deposits into them; ReturnZone settles on
    /// the phase event, which fires before this Update sees the new phase, so every deposit
    /// has landed by the time the forecast is drawn.
    /// Empty on the first night — Forecast falls back to the regen pool in that case.
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
