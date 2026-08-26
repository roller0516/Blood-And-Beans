using System.Collections.Generic;
using NUnit.Framework;

/// 기획서 3.2 (임대료·부채) / 3.3 (미납 페널티) / 5.5 (주문 생성) / 5.6.1 (인기 재료).
public class EconomyRuleTests
{
    // --- 3.2 임대료 표 ---

    [Test]
    public void RentTableMatchesTheDoc()
    {
        int[] expected = { 60, 100, 160, 250, 380, 560, 800 };
        for (var day = 1; day <= 7; day++)
            Assert.AreEqual(expected[day - 1], Rent.Due(day), $"{day}일차 임대료");
    }

    [Test]
    public void DaysOutsideTheTableClampToItsEnds()
    {
        Assert.AreEqual(60, Rent.Due(0));
        Assert.AreEqual(800, Rent.Due(8), "7일이 마지막이므로 그 뒤는 마지막 값을 유지한다");
    }

    // --- 3.2 부채 이월 (이자 없음) ---

    [Test]
    public void ShortfallCarriesWithoutInterest()
    {
        var rent = new Rent();

        Assert.AreEqual(40, rent.Settle(1, 40), "40만 있으면 40만 낸다");
        Assert.AreEqual(20, rent.Debt, "60 - 40 = 20이 이월된다");

        // 2일차: 100 + 이월 20 = 120. 이자가 붙지 않는다.
        Assert.AreEqual(120, rent.Settle(2, 500));
        Assert.AreEqual(0, rent.Debt);
    }

    [Test]
    public void PayingNothingIsNotNegativeDebt()
    {
        var rent = new Rent();
        Assert.AreEqual(0, rent.Settle(1, 0));
        Assert.AreEqual(60, rent.Debt);
    }

    [Test]
    public void OverpayingClearsButDoesNotCredit()
    {
        var rent = new Rent();
        Assert.AreEqual(60, rent.Settle(1, 1000), "낼 것보다 더 내지는 않는다");
        Assert.AreEqual(0, rent.Debt);
    }

    // --- 3.3 미납 연속 카운트와 단계 ---

    [Test]
    public void MissStreakDrivesThePenaltyTier()
    {
        var rent = new Rent();
        Assert.AreEqual(RentPenalty.None, rent.Penalty);

        rent.Settle(1, 0);
        Assert.AreEqual(RentPenalty.Tier1, rent.Penalty);
        rent.Settle(2, 0);
        Assert.AreEqual(RentPenalty.Tier2, rent.Penalty);
        rent.Settle(3, 0);
        Assert.AreEqual(RentPenalty.Tier3, rent.Penalty);
        rent.Settle(4, 0);
        Assert.AreEqual(RentPenalty.Tier3, rent.Penalty, "4회 행이 없으므로 3단계에서 멈춘다");
    }

    [Test]
    public void PayingInFullClearsTheStreak()
    {
        var rent = new Rent();
        rent.Settle(1, 0);
        rent.Settle(2, 0);
        Assert.AreEqual(RentPenalty.Tier2, rent.Penalty);

        rent.Settle(3, 10000);
        Assert.AreEqual(0, rent.MissStreak);
        Assert.AreEqual(RentPenalty.None, rent.Penalty, "다음 날 임대료를 내면 해제된다");
    }

    // --- 5.5 / 5.6.1 예보 ---

    static readonly Ingredient[] FullPool =
    {
        Ingredient.Milk, Ingredient.Cream, Ingredient.Chocolate,
        Ingredient.Almond, Ingredient.Berry, Ingredient.Ice,
    };

    static List<IReadOnlyList<Ingredient>> AllMenus()
    {
        var menus = new List<IReadOnlyList<Ingredient>>();
        foreach (var m in Menus.All) menus.Add(m.Parts);
        return menus;
    }

    static Forecast Build(int seed, IReadOnlyList<Ingredient> pool, int orders) =>
        Forecast.Build(seed, pool, AllMenus(), pool, orders);

    [Test]
    public void PopularIngredientsComeFromTonightsForestPoolOnly()
    {
        // 상비 재료에 보너스가 붙으면 모든 메뉴에 붙어서 아무 의미가 없다 (5.6.1).
        for (var seed = 0; seed < 50; seed++)
        {
            var f = Build(seed, FullPool, 8);
            foreach (var p in f.Popular)
            {
                Assert.IsFalse(Ingredients.IsStaple(p), $"seed {seed}: 상비 재료가 인기 재료로 뽑혔다");
                Assert.Contains(p, FullPool, $"seed {seed}: 리젠 풀 밖의 재료가 뽑혔다");
            }
        }
    }

    [Test]
    public void PopularIngredientCountIsTwoOrThree()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var n = Build(seed, FullPool, 8).Popular.Length;
            Assert.GreaterOrEqual(n, 2, $"seed {seed}");
            Assert.LessOrEqual(n, 3, $"seed {seed}");
        }
    }

    [Test]
    public void PopularIngredientsDoNotRepeat()
    {
        for (var seed = 0; seed < 50; seed++)
        {
            var f = Build(seed, FullPool, 8);
            CollectionAssert.AllItemsAreUnique(f.Popular, $"seed {seed}");
        }
    }

    [Test]
    public void BasicMenuStaysUnderTwentyPercentOfOrders()
    {
        // 5.5 규칙 4. 기본 메뉴는 상비 재료만으로 만들어지는 핫 아메리카노다 — 레시피가
        // 원두를 포함한 채로 넘어오므로 "빈 집합"이 아니라 "상비뿐"으로 판정해야 한다.
        var menus = AllMenus();
        var basic = new List<int>();
        for (var i = 0; i < menus.Count; i++)
        {
            var onlyStaples = true;
            foreach (var ing in menus[i]) onlyStaples &= Ingredients.IsStaple(ing);
            if (onlyStaples) basic.Add(i);
        }
        CollectionAssert.IsNotEmpty(basic, "핫 아메리카노가 후보에 있어야 이 테스트가 의미가 있다");

        const int orders = 10;
        for (var seed = 0; seed < 50; seed++)
        {
            var f = Build(seed, FullPool, orders);
            var count = 0;
            foreach (var o in f.Orders) if (basic.Contains(o)) count++;
            Assert.LessOrEqual(count, orders / 5, $"seed {seed}: 기본 메뉴가 20%를 넘었다");
        }
    }

    [Test]
    public void EveryOrderIsCraftableFromTonightsPool()
    {
        // 5.5 규칙 1. 오늘 밤 캘 수 없는 재료가 필요한 주문을 예보할 수는 없다.
        var pool = new[] { Ingredient.Milk, Ingredient.Ice };
        var menus = AllMenus();

        for (var seed = 0; seed < 30; seed++)
        {
            var f = Forecast.Build(seed, pool, menus, pool, 8);
            foreach (var o in f.Orders)
            {
                foreach (var ing in menus[o])
                {
                    if (Ingredients.IsStaple(ing)) continue;
                    CollectionAssert.Contains(pool, ing, $"seed {seed}: {ing}는 오늘 밤 안 난다");
                }
            }
        }
    }

    [Test]
    public void HeldSliceDoesNotBorrowMissingRegenIngredients()
    {
        var menus = new List<IReadOnlyList<Ingredient>>
        {
            new[] { Ingredient.Bean, Ingredient.Milk, Ingredient.Berry },
            new[] { Ingredient.Bean, Ingredient.Berry },
            new[] { Ingredient.Bean, Ingredient.Milk },
        };
        var pool = new[] { Ingredient.Milk };
        var held = new[] { Ingredient.Berry };

        for (var seed = 0; seed < 100; seed++)
        {
            var forecast = Forecast.Build(seed, pool, menus, held, 20);
            foreach (var order in forecast.Orders)
                Assert.That(order, Is.Not.EqualTo(0), "보유 후보가 리젠 풀을 빌려 두 재료 메뉴를 만들었다");
        }
    }

    [Test]
    public void OrdersAndRacesLineUp()
    {
        var f = Build(1234, FullPool, 8);
        Assert.AreEqual(8, f.Orders.Length);
        Assert.AreEqual(f.Orders.Length, f.Races.Length, "손님 한 명당 주문 하나");

        var total = 0;
        foreach (var c in f.RaceCounts) total += c;
        Assert.AreEqual(8, total, "종족 집계가 손님 수와 맞아야 예보 패널이 거짓말을 안 한다");
    }

    [Test]
    public void SameSeedGivesTheSameForecast()
    {
        // 서버가 뽑은 예보를 그대로 보여주려면 재현 가능해야 한다.
        var a = Build(77, FullPool, 8);
        var b = Build(77, FullPool, 8);
        CollectionAssert.AreEqual(a.Orders, b.Orders);
        CollectionAssert.AreEqual(a.Popular, b.Popular);
    }

    [Test]
    public void AnEmptyPoolDoesNotThrow()
    {
        // 첫 밤에는 팀 재고가 비어 있다.
        var f = Forecast.Build(5, new Ingredient[0], AllMenus(), new Ingredient[0], 8);
        Assert.IsNotNull(f.Orders);
        Assert.IsNotNull(f.Popular);
    }
}
