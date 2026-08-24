using Unity.Netcode;
using UnityEngine;

public enum Species { Zombie, Vampire, Ghost, Skeleton, Werewolf, Witch }

/// One waiting customer (doc 5.5). Holds preference tags and a draining patience
/// gauge; empty patience means they walk out and the order earns nothing.
public class Customer : NetworkBehaviour
{
    readonly NetworkVariable<Species> species = new();
    readonly NetworkVariable<MenuTag> require = new();   // every one of these must be present
    readonly NetworkVariable<MenuTag> anyOf = new();     // at least one of these, if set
    readonly NetworkVariable<int> minIngredients = new();
    readonly NetworkVariable<int> orderCount = new();
    readonly NetworkVariable<int> served = new();
    readonly NetworkVariable<float> patience = new();
    readonly NetworkVariable<int> team = new(-1);

    public Species Kind => species.Value;
    public int TeamId => team.Value;

    /// The queue spawns the customer first and assigns the species a moment later,
    /// so anything visual has to follow the value, not the spawn.
    public event System.Action<Species> SpeciesChanged;
    public MenuTag Require => require.Value;
    public MenuTag AnyOf => anyOf.Value;
    public int MinIngredients => minIngredients.Value;
    public int Remaining => orderCount.Value - served.Value;
    public float Patience => patience.Value;
    public float PatienceRatio => patience.Value / PatienceOf(species.Value);

    // ponytail: placeholders. Doc 14장 leaves patience spans and price spreads open —
    // only the relative ordering (zombie long/cheap, vampire short/rich, witch richest)
    // is specified. Move to DT_Passive/DT_Menu when the tables exist.
    public static float PatienceOf(Species s) => s switch
    {
        Species.Zombie => 90f,
        Species.Vampire => 30f,
        _ => 60f,
    };

    /// Species price weight. Economy owns the real formula — this is one input to it.
    public static float PriceWeightOf(Species s) => s switch
    {
        Species.Zombie => 0.7f,
        Species.Vampire => 1.4f,
        Species.Witch => 1.8f,
        _ => 1.0f,
    };

    public override void OnNetworkSpawn()
    {
        species.OnValueChanged += (_, now) => SpeciesChanged?.Invoke(now);
        SpeciesChanged?.Invoke(species.Value);
    }

    public void SetupServer(int teamId, Species s, MenuTag req, MenuTag any, int minParts, int count)
    {
        if (!IsServer) return;
        team.Value = teamId;
        var was = species.Value;
        species.Value = s;
        if (IsServer && was == s) SpeciesChanged?.Invoke(s);   // no change event fires for an equal write
        require.Value = req;
        anyOf.Value = any;
        minIngredients.Value = minParts;
        orderCount.Value = count;
        served.Value = 0;
        patience.Value = PatienceOf(s);
    }

    void Update()
    {
        if (!IsServer || patience.Value <= 0f) return;
        patience.Value = Mathf.Max(0f, patience.Value - Time.deltaTime);
    }

    /// Tag-only match — the customer never learns the menu's name (doc 7.2).
    public bool Accepts(MenuTag tags, int ingredientCount) =>
        (require.Value & tags) == require.Value &&
        (anyOf.Value == MenuTag.None || (anyOf.Value & tags) != MenuTag.None) &&
        ingredientCount >= minIngredients.Value;

    /// Returns true when this was the last item they wanted.
    public bool CountServedServer()
    {
        if (!IsServer) return false;
        served.Value++;
        return Remaining <= 0;
    }

    public void AddPatienceServer(float delta)
    {
        if (!IsServer) return;
        patience.Value = Mathf.Clamp(patience.Value + delta, 0f, PatienceOf(species.Value));
    }
}
