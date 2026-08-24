using System;
using System.Collections.Generic;

/// Customer races (design doc 5.5).
public enum Race { Zombie, Vampire, Ghost, Skeleton, Werewolf, Witch }

/// Next day's customer forecast, decided during the Transition phase (doc 5.6).
/// Server-side, seeded so every client can be told the same thing cheaply.
public class Forecast
{
    /// 2~3 popular ingredients, drawn ONLY from tonight's regen pool (5.6.1).
    public Ingredient[] Popular;

    /// One entry per customer: index into the `menus` list handed to Build().
    /// ponytail: indices instead of a MenuDef reference — Day/ owns MenuDef and it
    /// does not exist yet. Swap to MenuDef when it lands.
    public int[] Orders;

    /// Parallel to Orders.
    public Race[] Races;

    /// Counts per race, for the forecast panel header.
    public int[] RaceCounts = new int[6];

    // ponytail: doc 14장 #14 leaves popular-ingredient count undecided; 2~3 is the
    // doc's stated default. Same for the +30% each, which lives in SalePrice.
    const int MinPopular = 2, MaxPopular = 3;

    // ponytail: flat race weights, doc gives no distribution. Tune or table-ise later.
    static readonly Race[] RaceBag =
    {
        Race.Zombie, Race.Zombie, Race.Vampire, Race.Vampire,
        Race.Ghost, Race.Skeleton, Race.Werewolf, Race.Witch,
    };

    /// `menus` are ingredient sets holding only forest ingredients — 원두/빵 베이스
    /// are always stocked (7.1), so an empty set means basic hot americano.
    public static Forecast Build(
        int seed,
        IReadOnlyList<Ingredient> regenPool,
        IReadOnlyList<IReadOnlyList<Ingredient>> menus,
        IReadOnlyList<Ingredient> teamHeld,
        int orderCount)
    {
        var rng = new System.Random(seed);
        var f = new Forecast();

        // Popular ingredients come from the forest only (5.6.1) — a bonus on a staple
        // that is always in stock would apply to everything and mean nothing.
        f.Popular = PickPopular(rng, Forageable(regenPool));

        // 5.5 rule 1: only menus craftable from tonight's pool are candidates. Bean and
        // BreadBase are always stocked (7.1), so craftability must assume them present.
        var fromPool = Craftable(menus, regenPool, Staples);
        // 5.5 rule 3: the 30% slice also allows what teams already hold.
        var fromHeld = Craftable(menus, Array.Empty<Ingredient>(), Combine(teamHeld, Staples));
        if (fromPool.Count == 0) fromPool = fromHeld;
        if (fromHeld.Count == 0) fromHeld = fromPool;
        if (fromPool.Count == 0) { f.Orders = Array.Empty<int>(); f.Races = Array.Empty<Race>(); return f; }

        var americanoCap = orderCount / 5; // 20% ceiling (5.5 rule 4)
        var americanos = 0;
        f.Orders = new int[orderCount];
        f.Races = new Race[orderCount];

        for (var i = 0; i < orderCount; i++)
        {
            var bag = rng.NextDouble() < 0.7 ? fromPool : fromHeld;
            var race = PickRace(rng, bag, menus);
            var pick = PickForRace(rng, bag, menus, race);

            if (IsBasic(menus[pick]))
            {
                if (americanos >= americanoCap)
                    pick = PickNonBasic(rng, bag, menus, race, pick);
                if (IsBasic(menus[pick])) americanos++;
            }

            f.Orders[i] = pick;
            f.Races[i] = race;
            f.RaceCounts[(int)race]++;
        }
        return f;
    }

    static readonly Ingredient[] Staples = { Ingredient.Bean, Ingredient.BreadBase };

    /// The basic menu is the one made of nothing but staples — hot americano (doc 5.5
    /// rule 4). Callers hand over full recipes including 원두/빵 베이스, so testing for an
    /// empty set silently never matched and the 20% cap never applied to anything.
    static bool IsBasic(IReadOnlyList<Ingredient> menu)
    {
        if (menu == null || menu.Count == 0) return true;
        for (var i = 0; i < menu.Count; i++)
            if (!Ingredients.IsStaple(menu[i])) return false;
        return true;
    }

    static List<Ingredient> Forageable(IReadOnlyList<Ingredient> pool)
    {
        var outp = new List<Ingredient>();
        if (pool != null)
            foreach (var i in pool) if (!Ingredients.IsStaple(i)) outp.Add(i);
        return outp;
    }

    static List<Ingredient> Combine(IReadOnlyList<Ingredient> a, IReadOnlyList<Ingredient> b)
    {
        var outp = new List<Ingredient>();
        if (a != null) outp.AddRange(a);
        if (b != null) outp.AddRange(b);
        return outp;
    }

    static Ingredient[] PickPopular(System.Random rng, IReadOnlyList<Ingredient> pool)
    {
        var src = new List<Ingredient>(pool);
        var want = Math.Min(rng.Next(MinPopular, MaxPopular + 1), src.Count);
        var outp = new Ingredient[want];
        for (var i = 0; i < want; i++)
        {
            var k = rng.Next(src.Count);
            outp[i] = src[k];
            src.RemoveAt(k);
        }
        return outp;
    }

    /// Menu is craftable when every ingredient is in the pool (or, for the held
    /// slice, in the team's stock).
    static List<int> Craftable(
        IReadOnlyList<IReadOnlyList<Ingredient>> menus,
        IReadOnlyList<Ingredient> pool,
        IReadOnlyList<Ingredient> extra)
    {
        var ok = new List<int>();
        for (var m = 0; m < menus.Count; m++)
        {
            var all = true;
            for (var j = 0; j < menus[m].Count && all; j++)
                all = Has(pool, menus[m][j]) || Has(extra, menus[m][j]);
            if (all) ok.Add(m);
        }
        return ok;
    }

    static bool Has(IReadOnlyList<Ingredient> list, Ingredient i)
    {
        if (list == null) return false;
        for (var k = 0; k < list.Count; k++) if (list[k] == i) return true;
        return false;
    }

    // ponytail: linear scan for a non-basic menu. Candidate lists are ~10 long.
    static Race PickRace(System.Random rng, List<int> bag,
        IReadOnlyList<IReadOnlyList<Ingredient>> menus)
    {
        var start = rng.Next(RaceBag.Length);
        for (var n = 0; n < RaceBag.Length; n++)
        {
            var race = RaceBag[(start + n) % RaceBag.Length];
            for (var i = 0; i < bag.Count; i++)
                if (MatchesRace(menus[bag[i]], race)) return race;
        }
        return Race.Zombie;
    }

    static int PickForRace(System.Random rng, List<int> bag,
        IReadOnlyList<IReadOnlyList<Ingredient>> menus, Race race)
    {
        var start = rng.Next(bag.Count);
        for (var n = 0; n < bag.Count; n++)
        {
            var candidate = bag[(start + n) % bag.Count];
            if (MatchesRace(menus[candidate], race)) return candidate;
        }
        return bag[start];
    }

    static bool MatchesRace(IReadOnlyList<Ingredient> menu, Race race)
    {
        var tags = Menus.TagsOf(menu);
        return race switch
        {
            Race.Vampire => (tags & (MenuTag.Fruity | MenuTag.Blood)) != 0,
            Race.Ghost => (tags & MenuTag.Cold) != 0,
            Race.Skeleton => (tags & MenuTag.Hot) != 0,
            Race.Witch => menu.Count >= 3,
            _ => true,
        };
    }

    static int PickNonBasic(System.Random rng, List<int> bag,
        IReadOnlyList<IReadOnlyList<Ingredient>> menus, Race race, int fallback)
    {
        var start = rng.Next(bag.Count);
        for (var n = 0; n < bag.Count; n++)
        {
            var c = bag[(start + n) % bag.Count];
            if (!IsBasic(menus[c]) && MatchesRace(menus[c], race)) return c;
        }
        return fallback; // pool has nothing but americano; cap yields.
    }
}
