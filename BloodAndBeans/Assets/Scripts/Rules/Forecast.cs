using System;
using System.Collections.Generic;

/// 손님 종족 (기획서 5.5).
public enum Race { Zombie, Vampire, Ghost, Skeleton, Werewolf, Witch }

/// 다음 날 손님 예보. 전환 페이즈에서 결정된다 (기획서 5.6).
/// 서버 전용이며, 시드 기반이라 모든 클라이언트에 같은 결과를 싸게 전달할 수 있다.
public class Forecast
{
    /// 인기 재료 2~3종. 오직 오늘 밤 리젠 풀에서만 뽑는다 (5.6.1).
    public Ingredient[] Popular;

    /// 손님 한 명당 한 칸. Build()에 넘긴 `menus` 목록의 인덱스다.
    /// ponytail: MenuDef 참조 대신 인덱스를 쓴다 — MenuDef는 Day/ 소유인데 아직
    /// 없었다. 생기면 MenuDef로 바꾼다.
    public int[] Orders;

    /// Orders와 같은 순서로 대응한다.
    public Race[] Races;

    /// 예보 패널 헤더에 쓰는 종족별 인원수.
    public int[] RaceCounts = new int[6];

    // ponytail: 기획서 14장 #14에서 인기 재료 개수가 미결정이다. 2~3은 기획서가
    // 명시한 기본값이다. 개당 +30%도 마찬가지이며 그 값은 SalePrice에 있다.
    const int MinPopular = 2, MaxPopular = 3;

    // ponytail: 종족 가중치가 균등하다. 기획서에 분포가 없다. 나중에 조정하거나 표로 뺀다.
    static readonly Race[] RaceBag =
    {
        Race.Zombie, Race.Zombie, Race.Vampire, Race.Vampire,
        Race.Ghost, Race.Skeleton, Race.Werewolf, Race.Witch,
    };

    /// `menus`는 숲 재료만 담은 재료 집합이다. 원두/빵 베이스는 항상 상비이므로(7.1)
    /// 빈 집합은 기본 핫 아메리카노를 뜻한다.
    public static Forecast Build(
        int seed,
        IReadOnlyList<Ingredient> regenPool,
        IReadOnlyList<IReadOnlyList<Ingredient>> menus,
        IReadOnlyList<Ingredient> teamHeld,
        int orderCount)
    {
        var rng = new System.Random(seed);
        var f = new Forecast();

        // 인기 재료는 숲에서만 나온다 (5.6.1). 항상 재고가 있는 상비 재료에 보너스를 주면
        // 모든 메뉴에 똑같이 붙어서 아무 의미가 없다.
        f.Popular = PickPopular(rng, Forageable(regenPool));

        // 5.5 규칙 1: 오늘 밤 풀로 만들 수 있는 메뉴만 후보다. 원두와 빵 베이스는 항상
        // 상비이므로(7.1) 제작 가능 판정은 이 둘이 있다고 전제해야 한다.
        var fromPool = Craftable(menus, regenPool, Staples);
        // 5.5 규칙 3: 30% 몫은 팀이 이미 보유한 재료도 허용한다.
        var fromHeld = Craftable(menus, Array.Empty<Ingredient>(), Combine(teamHeld, Staples));
        if (fromPool.Count == 0) fromPool = fromHeld;
        if (fromHeld.Count == 0) fromHeld = fromPool;
        if (fromPool.Count == 0) { f.Orders = Array.Empty<int>(); f.Races = Array.Empty<Race>(); return f; }

        var americanoCap = orderCount / 5; // 20% 상한 (5.5 규칙 4)
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

    /// 기본 메뉴는 상비 재료만으로 만들어지는 메뉴, 즉 핫 아메리카노다 (기획서 5.5 규칙 4).
    /// 호출부는 원두/빵 베이스를 포함한 완전한 레시피를 넘기므로, 빈 집합으로 판정하면
    /// 아무것도 걸리지 않아 20% 상한이 어디에도 적용되지 않았다.
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

    /// 모든 재료가 풀에 있으면(보유분 몫이라면 팀 재고에 있으면) 제작 가능한 메뉴다.
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

    // ponytail: 기본 메뉴가 아닌 것을 선형 탐색한다. 후보 목록은 10개 안팎이다.
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
        return fallback; // 풀에 아메리카노밖에 없다. 상한을 양보한다.
    }
}
