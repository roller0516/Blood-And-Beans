using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// What you are carrying and what it costs you to carry it (design doc 6.7).
/// No slot cap and no weight cap — you can always take more, you just crawl.
///
/// Items are tracked individually, not as a single number: doc 6.6 spills a share of
/// the load onto the ground as a lootable pile, and you cannot drop what you never
/// recorded.
public class PlayerInventory : NetworkBehaviour
{
    [SerializeField] float capacity = 20f;
    [SerializeField] ItemBox pilePrefab;

    readonly NetworkList<int> items = new(null,
        NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);
    readonly NetworkVariable<float> carried = new(0f,
        NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

    MatchDirector director;

    public override void OnNetworkSpawn() => director = MatchDirector.Find();

    public float Carried => carried.Value;
    public float LoadRatio => carried.Value / capacity;
    public int Count => items.Count;

    /// Rent tier 3 shifts the band exactly one step against you (doc 3.3, night column).
    /// The weight→speed table itself is `LoadBands` in BB.Rules, so it can be checked
    /// against doc 6.7 without a scene.
    public float CurrentSpeedMultiplier
    {
        get
        {
            var team = GetComponent<PlayerTeam>();
            var ledger = director != null && team != null ? director.LedgerOf(team.Team) : null;
            return LoadBands.SpeedMultiplierShifted(LoadRatio, ledger != null && ledger.WeightBandShifted);
        }
    }

    public void AddServer(Ingredient item)
    {
        if (!IsServer) return;
        items.Add((int)item);
        carried.Value += Ingredients.WeightOf(item);
    }

    /// Missing the return zone at night's end costs half the load (doc 6.8).
    /// Lost outright — nobody can pick it up.
    public void LoseHalfServer()
    {
        if (!IsServer) return;

        var remaining = new List<Ingredient>();
        foreach (var item in items) remaining.Add((Ingredient)item);
        RandomLoss.TakeHalf(remaining, new System.Random(Random.Range(int.MinValue, int.MaxValue)));

        items.Clear();
        carried.Value = 0f;
        foreach (var item in remaining)
        {
            items.Add((int)item);
            carried.Value += Ingredients.WeightOf(item);
        }
    }

    /// Dashing a carrier at 80%+ load spills part of it on the floor (doc 6.6).
    /// The spill becomes a temporary box anyone can open (doc 6.5.4).
    public void DropShareServer(float share, Vector3 at)
    {
        if (!IsServer) return;

        var dropped = TakeOutServer(carried.Value * Mathf.Clamp01(share));
        if (dropped.Count == 0 || pilePrefab == null) return;

        var pile = Instantiate(pilePrefab, at + Vector3.up * 0.3f, Quaternion.identity);
        pile.NetworkObject.Spawn();
        pile.SeedServer(dropped);
    }

    /// Hands the whole bag over and empties it. The night's haul becomes the team's
    /// larder at the return zone (doc 2장), so the caller owns what comes back.
    public List<Ingredient> DrainServer()
    {
        var taken = new List<Ingredient>();
        if (!IsServer) return taken;

        foreach (var i in items) taken.Add((Ingredient)i);
        items.Clear();
        carried.Value = 0f;
        return taken;
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    public void DumpRpc()
    {
        var phase = director != null ? director.Phase : null;
        if (phase == null || phase.Current != Phase.Night || items.Count == 0) return;

        var dropped = DrainServer();
        if (pilePrefab == null) return;
        var pile = Instantiate(pilePrefab, transform.position + Vector3.up * 0.3f, Quaternion.identity);
        pile.NetworkObject.Spawn();
        pile.SeedServer(dropped);
    }

    /// Removes items until at least `weight` has been taken out, and returns them.
    /// Lightest first: taking the heaviest first overshot the target by up to 160%
    /// and always took the most valuable item, which the doc does not ask for.
    List<Ingredient> TakeOutServer(float weight)
    {
        var taken = new List<Ingredient>();
        if (!IsServer || weight <= 0f) return taken;

        var removed = 0f;
        while (removed < weight && items.Count > 0)
        {
            var lightest = 0;
            for (var i = 1; i < items.Count; i++)
                if (Ingredients.WeightOf((Ingredient)items[i]) <
                    Ingredients.WeightOf((Ingredient)items[lightest])) lightest = i;

            var item = (Ingredient)items[lightest];
            items.RemoveAt(lightest);
            removed += Ingredients.WeightOf(item);
            taken.Add(item);
        }

        carried.Value = Mathf.Max(0f, carried.Value - removed);
        return taken;
    }
}
