using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// Everything Economy needs to price one sale (doc 5.6.2). Day knows the gauge,
/// the recipe and the customer; it does not know the rent, the forecast or the
/// bean grade table, so it hands this over and stays out of the arithmetic.
public struct ServeInfo
{
    public MenuId Menu;
    public Ingredient[] Recipe;
    public float GaugeMultiplier;   // 1.3 / 1.0 / 0.7 / 0.3 (burnt)
    public bool Burnt;
    public Species Kind;
    public float SpeciesPriceWeight;
    public int BasePrice;
}

/// The waiting line (doc 5.5). Spawns customers, times them out, and validates a
/// served item against an order by TAG only.
public class CustomerQueue : NetworkBehaviour
{
    [SerializeField] Customer customerPrefab;
    [SerializeField] GamePhase phase;
    [SerializeField] int maxWaiting = 4;
    [SerializeField] float spawnSeconds = 8f;   // ponytail: placeholder, doc 14장 #1
    [SerializeField] float slotSpacing = 1.5f;

    // ponytail: burnt-sale patience hit is doc 14장 #6, still undecided.
    [SerializeField] float burntPatiencePenalty = 10f;

    readonly List<Customer> waiting = new();
    public IReadOnlyList<Customer> Waiting => waiting;
    double nextSpawn;

    /// Economy subscribes to this. Day never computes a final price itself.
    /// Per-queue, not static: with two cafes a static event booked every team's
    /// sales into whichever till happened to subscribe.
    public event System.Action<ServeInfo> Served;

    void Update()
    {
        if (!IsServer) return;

        // Customers only exist during the day.
        if (phase != null && phase.Current != Phase.Day) { ClearAll(); return; }

        for (int i = waiting.Count - 1; i >= 0; i--)
        {
            if (waiting[i] == null) { waiting.RemoveAt(i); continue; }
            if (waiting[i].Patience > 0f) continue;
            Leave(i);                      // out of patience: leaves, revenue 0
        }

        if (waiting.Count >= maxWaiting || planned.Count == 0) return;
        if (NetworkManager.ServerTime.Time < nextSpawn) return;
        nextSpawn = NetworkManager.ServerTime.Time + spawnSeconds;
        Spawn();
    }

    void Spawn()
    {
        if (customerPrefab == null) return;

        var c = Instantiate(customerPrefab, SlotPosition(waiting.Count), transform.rotation);
        c.NetworkObject.SpawnWithObservers = false;
        c.NetworkObject.Spawn();
        var team = Cafe.Of(this)?.TeamId ?? -1;
        MatchDirector.Find()?.ShowToTeamServer(c.NetworkObject, team);
        waiting.Add(c);

        var next = planned.Dequeue();
        var menu = Menus.All[next.menu];
        var count = next.species == Species.Werewolf ? Random.Range(2, 4) : 1;
        c.SetupServer(team, next.species, Menus.TagsOf(menu.Parts), MenuTag.None, menu.Parts.Length, count);
    }

    readonly System.Collections.Generic.Queue<(Species species, int menu)> planned = new();

    /// The transition screen promises tomorrow's customer mix (doc 5.6), so the queue
    /// has to actually serve that mix. Economy builds it; this consumes it in order.
    public void SetDayPlanServer(Forecast forecast)
    {
        if (!IsServer) return;
        planned.Clear();
        if (forecast?.Races == null || forecast.Orders == null) return;
        var count = Mathf.Min(forecast.Races.Length, forecast.Orders.Length);
        for (var i = 0; i < count; i++)
            if (forecast.Orders[i] >= 0 && forecast.Orders[i] < Menus.All.Length)
                planned.Enqueue(((Species)(int)forecast.Races[i], forecast.Orders[i]));
    }

    Vector3 SlotPosition(int index) => transform.position + transform.right * (index * slotSpacing);

    void Leave(int index)
    {
        var c = waiting[index];
        waiting.RemoveAt(index);
        if (c != null && c.NetworkObject.IsSpawned) c.NetworkObject.Despawn();
        Reflow();
    }

    void ClearAll()
    {
        for (int i = waiting.Count - 1; i >= 0; i--) Leave(i);
    }

    void Reflow()
    {
        for (int i = 0; i < waiting.Count; i++)
            if (waiting[i] != null) waiting[i].transform.position = SlotPosition(i);
    }

    public void RestoreFrontServer()
    {
        if (!IsServer || waiting.Count == 0) return;
        waiting[0].AddPatienceServer(Customer.PatienceOf(waiting[0].Kind) * 0.25f);
    }

    /// Server-authoritative serving. Returns true when someone took the item.
    /// Matching is by tag, never by menu name (doc 7.2).
    public bool TryServeServer(HeldItem item)
    {
        if (!IsServer || !item.IsProduct || item.Recipe == null) return false;

        var tags = Menus.TagsOf(item.Recipe);
        var index = waiting.FindIndex(c => c != null && c.Accepts(tags, item.Recipe.Length));
        if (index < 0) return false;

        var c = waiting[index];
        Served?.Invoke(new ServeInfo
        {
            Menu = item.Menu,
            Recipe = item.Recipe,
            GaugeMultiplier = item.GaugeMultiplier,
            Burnt = item.Burnt,
            Kind = c.Kind,
            SpeciesPriceWeight = Customer.PriceWeightOf(c.Kind),
            BasePrice = Menus.BasePriceOf(item.Menu),
        });

        // Selling something burnt sours the whole room (doc 5.3).
        if (item.Burnt)
            foreach (var w in waiting)
                if (w != null) w.AddPatienceServer(-burntPatiencePenalty);

        Cafe.Of(this)?.Dishes?.SoilServer();
        if (c.CountServedServer()) Leave(index);
        return true;
    }

}
