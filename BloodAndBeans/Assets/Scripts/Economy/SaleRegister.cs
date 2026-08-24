using System.Linq;
using Unity.Netcode;
using UnityEngine;

/// The till. Day/ announces a serve, this turns it into money and books it.
/// Kept as its own file so Day/ never has to know Economy/ exists (see 스크립트 구조.md).
public class SaleRegister : NetworkBehaviour
{
    // team comes from the cafe this till belongs to, so the two can never disagree

    static readonly Ingredient[] NoPopular = new Ingredient[0];

    Scoreboard board;
    CustomerQueue queue;
    int team;

    public Ingredient[] Popular { get; set; } = NoPopular;
    public int LastSale { get; private set; }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        board = FindFirstObjectByType<Scoreboard>();

        var cafe = Cafe.Of(this);
        team = cafe != null ? cafe.TeamId : 0;
        queue = cafe != null ? cafe.Queue : null;
        if (queue != null) queue.Served += Book;
    }

    public override void OnNetworkDespawn()
    {
        if (queue != null) queue.Served -= Book;
    }

    void Book(ServeInfo info)
    {
        if (!IsServer) return;

        var recipe = info.Recipe ?? NoPopular;

        // Dessert is anything built on bread, and it ignores bean grade entirely (5.6.2).
        var isDessert = recipe.Contains(Ingredient.BreadBase);
        var grade = recipe.Contains(Ingredient.BloodBean) ? BeanGrade.Blood : BeanGrade.Normal;

        var price = SalePrice.Calculate(
            info.BasePrice, GaugeOf(info), grade, isDessert, recipe, Popular);

        // Species weighting is the customer's own multiplier (5.5), not part of 5.6.2.
        LastSale = Mathf.RoundToInt(price * Mathf.Max(0f, info.SpeciesPriceWeight));
        board?.AddSale(team, LastSale);
    }

    /// ServeInfo carries the multiplier; SalePrice wants the verdict that produced it.
    static Gauge GaugeOf(ServeInfo info) =>
        info.Burnt ? Gauge.Burnt :
        info.GaugeMultiplier >= 1.3f ? Gauge.Perfect :
        info.GaugeMultiplier >= 1.0f ? Gauge.Good :
                                       Gauge.Miss;
}
