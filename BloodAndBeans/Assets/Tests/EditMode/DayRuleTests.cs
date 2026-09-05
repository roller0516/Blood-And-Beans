using System.Collections.Generic;
using NUnit.Framework;

/// 기획서 5.2 / 5.6.1 / 5.6.2 / 7.1 / 7.2. 여기 기대값은 전부 구현이 아니라 기획서에서
/// 읽어 온 것이다. 구조상 코드와 항상 일치하는 테스트는 아무것도 검증하지 못한다.
public class DayRuleTests
{
    static readonly Ingredient[] Popular = { Ingredient.Chocolate, Ingredient.Ice, Ingredient.Berry };

    static readonly Ingredient[] HotAmericano = { };
    static readonly Ingredient[] CafeLatte = { Ingredient.Milk };
    static readonly Ingredient[] IcedLatte = { Ingredient.Milk, Ingredient.Ice };
    static readonly Ingredient[] IcedMocha = { Ingredient.Milk, Ingredient.Chocolate, Ingredient.Ice };
    static readonly Ingredient[] BerryTart = { Ingredient.Berry };

    static int Coffee(IReadOnlyList<Ingredient> menu) =>
        SalePrice.Calculate(100, Gauge.Good, BeanGrade.Normal, false, menu, Popular);

    // --- 5.6.1 인기 재료 보너스 표 ---

    [Test]
    public void PopularBonusMatchesTheDocTable()
    {
        Assert.AreEqual(100, Coffee(HotAmericano), "핫 아메리카노 +0%");
        Assert.AreEqual(100, Coffee(CafeLatte), "카페라떼 +0%");
        Assert.AreEqual(130, Coffee(IcedLatte), "아이스라떼 +30%");
        Assert.AreEqual(160, Coffee(IcedMocha), "아이스모카 +60%");
        Assert.AreEqual(130,
            SalePrice.Calculate(100, Gauge.Good, BeanGrade.Normal, true, BerryTart, Popular),
            "베리 타르트 +30%");
    }

    [Test]
    public void BonusesAreSummedNotMultiplied()
    {
        // 두 개면 ×1.6이다. 곱이면 1.3×1.3 = ×1.69가 되어 169가 나온다 (5.6.2).
        Assert.AreEqual(160, Coffee(IcedMocha));
        Assert.AreNotEqual(169, Coffee(IcedMocha));
    }

    // --- 5.2 완성 게이지 배수 ---

    [Test]
    public void GaugeMultipliers()
    {
        Assert.AreEqual(130, SalePrice.Calculate(100, Gauge.Perfect, BeanGrade.Normal, false, HotAmericano, Popular));
        Assert.AreEqual(100, SalePrice.Calculate(100, Gauge.Good, BeanGrade.Normal, false, HotAmericano, Popular));
        Assert.AreEqual(70, SalePrice.Calculate(100, Gauge.Miss, BeanGrade.Normal, false, HotAmericano, Popular));
        Assert.AreEqual(30, SalePrice.Calculate(100, Gauge.Burnt, BeanGrade.Normal, false, HotAmericano, Popular));
    }

    // --- 5.6.2 원두 등급은 커피 한정 ---

    [Test]
    public void BeanGradeAppliesToCoffeeOnly()
    {
        Assert.AreEqual(150, SalePrice.Calculate(100, Gauge.Good, BeanGrade.Blood, false, HotAmericano, Popular));
        Assert.AreEqual(100, SalePrice.Calculate(100, Gauge.Good, BeanGrade.Blood, true, HotAmericano, Popular),
            "디저트에는 원두 등급 배수가 붙지 않는다");
    }

    [Test]
    public void AllMultipliersStack()
    {
        // 100 × 1.3 × 1.5 × 1.6 = 312
        Assert.AreEqual(312,
            SalePrice.Calculate(100, Gauge.Perfect, BeanGrade.Blood, false, IcedMocha, Popular));
    }

    [Test]
    public void PopularCountIgnoresIngredientsTheMenuDoesNotHave()
    {
        Assert.AreEqual(0, SalePrice.PopularCount(CafeLatte, Popular));
        Assert.AreEqual(2, SalePrice.PopularCount(IcedMocha, Popular));
        Assert.AreEqual(0, SalePrice.PopularCount(null, Popular));
        Assert.AreEqual(0, SalePrice.PopularCount(IcedMocha, null));
    }

    // --- 7.2 메뉴는 재료 집합이고 순서를 모른다 ---

    [Test]
    public void MenuMatchIgnoresOrder()
    {
        Assert.AreEqual(MenuId.CafeMocha,
            Menus.Match(new[] { Ingredient.Chocolate, Ingredient.Bean, Ingredient.Milk }));
        Assert.AreEqual(MenuId.CafeMocha,
            Menus.Match(new[] { Ingredient.Bean, Ingredient.Milk, Ingredient.Chocolate }));
    }

    [Test]
    public void BloodBeanIsASubstitutionNotItsOwnMenu()
    {
        Assert.AreEqual(MenuId.CafeLatte,
            Menus.Match(new[] { Ingredient.BloodBean, Ingredient.Milk }),
            "블러드 빈으로 만든 라떼도 라떼다");
    }

    [Test]
    public void UnknownCombinationIsLegalButHasNoMenu()
    {
        var id = Menus.Match(new[] { Ingredient.Almond, Ingredient.Ice });
        Assert.AreEqual(MenuId.None, id);
        Assert.AreEqual(0, Menus.BasePriceOf(id));
    }

    [Test]
    public void HotIsTheDefaultTemperature()
    {
        // 얼음이 있어야 차갑다 (7.1).
        Assert.IsTrue((Menus.TagsOf(new[] { Ingredient.Bean, Ingredient.Milk }) & MenuTag.Hot) != 0);
        var iced = Menus.TagsOf(new[] { Ingredient.Bean, Ingredient.Ice });
        Assert.IsTrue((iced & MenuTag.Cold) != 0);
        Assert.IsTrue((iced & MenuTag.Hot) == 0);
    }

    [Test]
    public void BloodBeanCarriesBothBloodAndCoffeeTags()
    {
        var tags = Menus.TagsOf(new[] { Ingredient.BloodBean });
        Assert.IsTrue((tags & MenuTag.Blood) != 0, "뱀파이어가 찾는 붉은 것 (5.5)");
        Assert.IsTrue((tags & MenuTag.Coffee) != 0);
    }

    // --- 7.1 상비 재료 ---

    [Test]
    public void StaplesAreBeanAndBreadBaseOnly()
    {
        Assert.IsTrue(Ingredients.IsStaple(Ingredient.Bean));
        Assert.IsTrue(Ingredients.IsStaple(Ingredient.BreadBase));
        Assert.IsFalse(Ingredients.IsStaple(Ingredient.BloodBean), "블러드 빈은 3등급 박스에서만 나온다");
        Assert.IsFalse(Ingredients.IsStaple(Ingredient.Milk));
    }

    [Test]
    public void NothingWeighsNothing()
    {
        Assert.AreEqual(0f, Ingredients.WeightOf(Ingredient.None));
        Assert.Less(Ingredients.WeightOf(Ingredient.Ice), Ingredients.WeightOf(Ingredient.BloodBean),
            "얼음은 가볍고 블러드 빈은 무겁다 (6.7)");
    }

    // --- 5.1 디저트는 조리대에서 조립한다 ---

    [Test]
    public void DessertToppingsComeFromTheMenuTable()
    {
        // 기획서 7.2 디저트 4종이 빵 베이스와 함께 쓰는 재료가 곧 얹을 수 있는 것이다.
        Assert.IsTrue(Menus.IsDessertTopping(Ingredient.Chocolate), "초코 브라우니");
        Assert.IsTrue(Menus.IsDessertTopping(Ingredient.Almond), "아몬드 쿠키");
        Assert.IsTrue(Menus.IsDessertTopping(Ingredient.Cream), "크림 케이크");
        Assert.IsTrue(Menus.IsDessertTopping(Ingredient.Berry), "베리 타르트");

        Assert.IsFalse(Menus.IsDessertTopping(Ingredient.Ice), "얼음은 디저트 메뉴에 없다");
        Assert.IsFalse(Menus.IsDessertTopping(Ingredient.Bean), "원두는 커피 쪽이다");
        Assert.IsFalse(Menus.IsDessertTopping(Menus.DessertBase), "바탕은 자기 위에 못 얹는다");
    }

    [Test]
    public void ADessertNeverNeedsMorePartsThanTheMenuTableSays()
    {
        // 조리대가 얹기를 멈추는 상한이다. 표보다 커지면 오븐 레인에 안 들어가는 덩어리가
        // 만들어지고, 작아지면 만들 수 없는 디저트가 생긴다.
        Assert.AreEqual(2, Menus.MaxDessertParts, "디저트는 바탕 + 재료 하나다 (7.2)");
    }

    [Test]
    public void AssembledDessertStillMatchesItsMenu()
    {
        // 조리대에서 조립한 것이 오븐에서 그대로 펼쳐진다 (`Station.Insert`).
        Assert.AreEqual(MenuId.ChocoBrownie,
            Menus.Match(new[] { Menus.DessertBase, Ingredient.Chocolate }));
    }

    // --- 4장 하루의 구조 ---

    [Test]
    public void DayPhaseTableMatchesTheDoc()
    {
        Assert.AreEqual(120f, DayPhases.NightSeconds);
        Assert.AreEqual(120f, DayPhases.DaySeconds);
        Assert.AreEqual(10f, DayPhases.TransitionSeconds);
        Assert.AreEqual(7, DayPhases.TotalDays);

        // 7일 × (120 + 120 + 10) = 1,750초 ≈ 29분 10초 (기획서 4장).
        Assert.AreEqual(1750f, DayPhases.TotalDays *
            (DayPhases.NightSeconds + DayPhases.DaySeconds + DayPhases.TransitionSeconds));
    }

    [Test]
    public void CookingOnlyAdvancesDuringDay()
    {
        Assert.IsTrue(Station.ShouldBeginGauge(Phase.Day, StationState.Cooking, 0f));
        Assert.IsFalse(Station.ShouldBeginGauge(Phase.Night, StationState.Cooking, 0f));
        Assert.IsFalse(Station.ShouldBeginGauge(Phase.Transition, StationState.Cooking, 0f));
        Assert.IsFalse(Station.ShouldBeginGauge(Phase.Day, StationState.Idle, 0f));
    }
}
