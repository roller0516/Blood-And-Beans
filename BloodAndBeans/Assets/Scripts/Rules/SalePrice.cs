using System.Collections.Generic;

/// Completion gauge judgement (design doc 5.2). Burnt = sold burnt (5.3 "판다").
public enum Gauge { Perfect, Good, Miss, Burnt }

/// Bean grade substitution (7.2). Dessert ignores this entirely (5.6.2).
public enum BeanGrade { Normal, Blood }

/// Final sale price, design doc 5.6.2:
///   price = base x gauge x beanGrade x (1 + sum of popular bonuses)
/// Stateless and UnityEngine-free so SalePriceSelfCheck can run it anywhere.
public static class SalePrice
{
    public const float PopularBonus = 0.30f;

    // ponytail: doc 14장 #12 leaves the top-grade bean multiplier undecided.
    // 1.5 is a placeholder; move to DT_Bean when the number lands.
    public const float BloodBeanMultiplier = 1.5f;

    public static float GaugeMultiplier(Gauge g) => g switch
    {
        Gauge.Perfect => 1.3f,
        Gauge.Good => 1.0f,
        Gauge.Miss => 0.7f,
        _ => 0.3f,
    };

    /// Counts how many popular ingredients the menu contains. Duplicates in the
    /// menu are not a thing (menus are ingredient sets), so a flat scan is enough.
    public static int PopularCount(IReadOnlyList<Ingredient> menu, IReadOnlyList<Ingredient> popular)
    {
        if (menu == null || popular == null) return 0;
        var n = 0;
        for (var i = 0; i < popular.Count; i++)
            for (var j = 0; j < menu.Count; j++)
                if (menu[j] == popular[i]) { n++; break; }
        return n;
    }

    /// The one that matters. basePrice comes from the menu table; menu lists only
    /// the forest ingredients (원두/빵 베이스 are always stocked, 7.1).
    public static int Calculate(
        int basePrice,
        Gauge gauge,
        BeanGrade grade,
        bool isDessert,
        IReadOnlyList<Ingredient> menu,
        IReadOnlyList<Ingredient> popular)
    {
        // Bonuses are summed and applied ONCE, not multiplied (5.6.2).
        // ponytail: doc 14장 #15 — undecided whether BloodBean/UpgradePart can be
        // popular at all. Counted as-is here; filter in Forecast if the rule says no.
        var bonus = 1f + PopularCount(menu, popular) * PopularBonus;
        var bean = (!isDessert && grade == BeanGrade.Blood) ? BloodBeanMultiplier : 1f;

        var raw = basePrice * GaugeMultiplier(gauge) * bean * bonus;
        return (int)System.Math.Round(raw, System.MidpointRounding.AwayFromZero);
    }
}
