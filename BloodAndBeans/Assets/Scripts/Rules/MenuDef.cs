using System.Collections.Generic;
using System.Linq;

/// Menu identity. The customer system never sees these — it matches tags only (doc 7.2).
public enum MenuId
{
    None = -1,
    HotAmericano, IcedAmericano, CafeLatte, Einspanner, CafeMocha, IcedLatte,
    ChocoBrownie, AlmondCookie, CreamCake, BerryTart,
}

[System.Flags]
public enum MenuTag
{
    None = 0,
    Hot = 1, Cold = 2,
    Milky = 4, Rich = 8, Sweet = 16, Nutty = 32, Fruity = 64,
    Blood = 128,
    Coffee = 256, Dessert = 512,
}

public readonly struct MenuDef
{
    public readonly MenuId Id;
    public readonly Ingredient[] Parts;
    public readonly int BasePrice;

    public MenuDef(MenuId id, int basePrice, params Ingredient[] parts)
    {
        Id = id; BasePrice = basePrice; Parts = parts;
    }
}

/// The 10 menus of doc 7.2 as ingredient sets. Both Day/ and Economy/ read this.
public static class Menus
{
    // ponytail: prices are placeholders (doc 14장 has no price table). The only fixed
    // constraint is that americano must not keep up with rent — move to DT_Menu later.
    public static readonly MenuDef[] All =
    {
        new(MenuId.HotAmericano,  30, Ingredient.Bean),
        new(MenuId.IcedAmericano, 35, Ingredient.Bean, Ingredient.Ice),
        new(MenuId.CafeLatte,     50, Ingredient.Bean, Ingredient.Milk),
        new(MenuId.Einspanner,    60, Ingredient.Bean, Ingredient.Cream),
        new(MenuId.CafeMocha,     70, Ingredient.Bean, Ingredient.Milk, Ingredient.Chocolate),
        new(MenuId.IcedLatte,     65, Ingredient.Bean, Ingredient.Milk, Ingredient.Ice),
        new(MenuId.ChocoBrownie,  55, Ingredient.BreadBase, Ingredient.Chocolate),
        new(MenuId.AlmondCookie,  50, Ingredient.BreadBase, Ingredient.Almond),
        new(MenuId.CreamCake,     65, Ingredient.BreadBase, Ingredient.Cream),
        new(MenuId.BerryTart,     70, Ingredient.BreadBase, Ingredient.Berry),
    };

    /// Blood bean is a bean substitution, not its own menu (doc 7.2).
    /// The grade multiplier it earns is Economy's business, not the menu table's.
    public static Ingredient Normalize(Ingredient i) =>
        i == Ingredient.BloodBean ? Ingredient.Bean : i;

    static MenuTag TagOf(Ingredient i) => i switch
    {
        Ingredient.Milk => MenuTag.Milky,
        Ingredient.Cream => MenuTag.Rich,
        Ingredient.Chocolate => MenuTag.Sweet,
        Ingredient.Almond => MenuTag.Nutty,
        Ingredient.Berry => MenuTag.Fruity,
        Ingredient.Ice => MenuTag.Cold,
        Ingredient.BloodBean => MenuTag.Blood | MenuTag.Coffee,
        Ingredient.Bean => MenuTag.Coffee,
        Ingredient.BreadBase => MenuTag.Dessert,
        _ => MenuTag.None,
    };

    /// Hot is the default temperature — ice is what makes a drink cold (doc 7.1).
    public static MenuTag TagsOf(IEnumerable<Ingredient> parts)
    {
        var tags = MenuTag.None;
        foreach (var p in parts) tags |= TagOf(p);
        if ((tags & MenuTag.Cold) == 0) tags |= MenuTag.Hot;
        return tags;
    }

    /// Set match, ignoring order. Returns None when the combination isn't a menu —
    /// which is legal: it still has tags, it just has no base price.
    public static MenuId Match(IEnumerable<Ingredient> parts)
    {
        var got = parts.Select(Normalize).OrderBy(i => (int)i).ToArray();
        foreach (var m in All)
        {
            if (m.Parts.Length != got.Length) continue;
            var want = m.Parts.OrderBy(i => (int)i);
            if (want.SequenceEqual(got)) return m.Id;
        }
        return MenuId.None;
    }

    public static int BasePriceOf(MenuId id)
    {
        foreach (var m in All) if (m.Id == id) return m.BasePrice;
        return 0;
    }
}
