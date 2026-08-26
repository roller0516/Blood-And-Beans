using System.Collections.Generic;
using System.Linq;

/// 메뉴 식별자. 손님 시스템은 이 값을 보지 않고 태그만 대조한다 (기획서 7.2).
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

/// 기획서 7.2의 메뉴 10종을 재료 집합으로 표현한 표. Day/와 Economy/가 함께 읽는다.
public static class Menus
{
    // ponytail: 가격은 임시값이다 (기획서 14장에 가격표가 없다). 확정된 제약은
    // 아메리카노만으로는 임대료를 감당할 수 없어야 한다는 것뿐이다. 나중에 DT_Menu로 옮긴다.
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

    /// 블러드빈은 별도 메뉴가 아니라 원두 대체재다 (기획서 7.2).
    /// 등급 배율은 메뉴 표가 아니라 Economy가 맡는다.
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

    /// 기본 온도는 뜨거움이다. 음료를 차갑게 만드는 것은 얼음이다 (기획서 7.1).
    public static MenuTag TagsOf(IEnumerable<Ingredient> parts)
    {
        var tags = MenuTag.None;
        foreach (var p in parts) tags |= TagOf(p);
        if ((tags & MenuTag.Cold) == 0) tags |= MenuTag.Hot;
        return tags;
    }

    /// 순서를 무시한 집합 비교. 메뉴가 아닌 조합이면 None을 돌려준다 —
    /// 이건 정상이다. 태그는 그대로 있고 기본가만 없을 뿐이다.
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
