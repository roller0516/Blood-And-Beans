using NUnit.Framework;
using UnityEngine;

public class DayUITests
{
    [Test]
    public void FacilityPromptsFollowDishAndIngredientState()
    {
        Assert.IsFalse(PlayerInteractor.CanUseFacility(FacilityKind.Coffee, CarryView.Nothing));
        Assert.IsTrue(PlayerInteractor.CanUseFacility(FacilityKind.Sink, CarryView.Nothing, 1));
        Assert.IsFalse(PlayerInteractor.CanUseFacility(FacilityKind.Sink, CarryView.Nothing, 0));
        Assert.IsTrue(PlayerInteractor.CanUseFacility(FacilityKind.Beans, CarryView.Of(HeldItem.Dish(false))));
        Assert.IsFalse(PlayerInteractor.CanUseFacility(FacilityKind.Beans, CarryView.Of(HeldItem.Dish(true))));
        var cup = HeldItem.Of(Ingredient.Bean); cup.HasDish = true;
        Assert.IsTrue(PlayerInteractor.CanUseFacility(FacilityKind.Coffee, CarryView.Of(cup)));
        Assert.IsFalse(PlayerInteractor.CanUseFacility(FacilityKind.Oven, CarryView.Of(cup)));
        Assert.IsFalse(PlayerInteractor.CanUseFacility(FacilityKind.Sink, CarryView.Of(HeldItem.Dish(false))));
        Assert.IsTrue(PlayerInteractor.CanUseFacility(FacilityKind.Sink, CarryView.Of(HeldItem.Dish(false, true))));
    }
    [Test]
    public void ExpectedPriceUsesPopularAndRaceWithoutGaugeBonus()
    {
        var menu = new MenuDef(MenuId.CafeLatte, 100, Ingredient.Bean, Ingredient.Milk);
        Assert.AreEqual(130, UICustomerOrder.ExpectedPrice(menu, Race.Ghost, new[] { Ingredient.Milk }));
        Assert.AreEqual(Mathf.RoundToInt(130 * Customer.PriceWeightOf(Race.Witch)),
            UICustomerOrder.ExpectedPrice(menu, Race.Witch, new[] { Ingredient.Milk }));
        Assert.AreEqual(100, UICustomerOrder.ExpectedPrice(menu, Race.Ghost, null));
    }
    [Test]
    public void OrdersAreHiddenOutsideCafeRegardlessOfFloorHeight()
    {
        var bounds = new Bounds(new Vector3(10, -1, 20), new Vector3(8, .2f, 6));
        Assert.IsTrue(UICustomerOrder.InsideFloor(bounds, new Vector3(10, 2, 20)));
        Assert.IsFalse(UICustomerOrder.InsideFloor(bounds, new Vector3(15, 2, 20)));
        Assert.IsFalse(UICustomerOrder.InsideFloor(bounds, new Vector3(10, 2, 24)));
    }
}
