using NUnit.Framework;

/// 손·조리대에 놓인 것의 표시값 (기획서 5.1: 낮의 조작은 재료를 옮기는 것이 전부다).
/// 복제되는 값이라 틀리면 팀원 화면에 남의 손이 잘못 뜬다.
public class CarryViewTests
{
    [Test]
    public void NothingIsEmpty()
    {
        var view = CarryView.Nothing;

        Assert.IsTrue(view.Empty);
        Assert.AreEqual("빈손", view.Label);
    }

    [Test]
    public void DefaultStructIsNotMistakenForMilk()
    {
        // Ingredient.None과 MenuId.None이 0이 아니라 -1이라 default(CarryView)는
        // "우유를 들고 있음"으로 읽힌다. 초기값을 명시해야 하는 이유가 이것이다.
        Assert.AreEqual(Ingredient.Milk, default(CarryView).Ingredient,
            "default가 우유인 것은 사실이다 — 그래서 Nothing을 써야 한다");
        Assert.AreEqual(Ingredient.None, CarryView.Nothing.Ingredient);
    }

    [Test]
    public void IngredientInHandShowsIngredient()
    {
        var view = CarryView.Of(new HeldItem { Ingredient = Ingredient.Berry });

        Assert.IsFalse(view.Empty);
        Assert.IsFalse(view.IsProduct);
        Assert.AreEqual(Ingredient.Berry.ToString(), view.Label);
    }

    [Test]
    public void ShelfIngredientIsNotAProduct()
    {
        // 재료 칸이 자기 재고를 그릴 때 쓰는 길이다 (`IngredientShelf.SlotAt`). 완성품으로
        // 새면 선반 위에 커피잔이 늘어선다.
        var view = CarryView.Of(Ingredient.Bean);

        Assert.IsFalse(view.Empty);
        Assert.IsFalse(view.IsProduct);
        Assert.AreEqual(Ingredient.Bean, view.Ingredient);
        Assert.AreEqual(MenuId.None, view.Menu);
    }

    [Test]
    public void ProductShowsMenuName()
    {
        var view = CarryView.Of(new HeldItem
        {
            IsProduct = true,
            Menu = MenuId.CafeLatte,
            Recipe = new[] { Ingredient.Bean, Ingredient.Milk },
        });

        Assert.IsTrue(view.IsProduct);
        Assert.IsFalse(view.Empty);
        Assert.AreEqual(MenuId.CafeLatte.ToString(), view.Label);
    }

    [Test]
    public void BurntProductIsMarked()
    {
        var view = CarryView.Of(new HeldItem
        {
            IsProduct = true,
            Menu = MenuId.ChocoBrownie,
            Burnt = true,
        });

        StringAssert.Contains("탄 것", view.Label);
    }

    [Test]
    public void ProductThatIsNotOnTheMenuStillShowsSomething()
    {
        // Menus.Match는 메뉴 표에 없는 조합에 None을 돌려준다. 그것을 빈손으로 그리면
        // 들고 있는 것이 화면에서 사라진다.
        var view = CarryView.Of(new HeldItem { IsProduct = true, Menu = MenuId.None });

        Assert.IsFalse(view.Empty);
        Assert.AreNotEqual("빈손", view.Label);
    }

    [Test]
    public void AssemblyShowsHowManyIngredientsAreOnIt()
    {
        // 조리대에서 조립 중인 디저트 (기획서 5.1). 완성품도 낱개 재료도 아니라서, 이
        // 구분이 없으면 재료를 얹어도 화면에는 빵 베이스 하나로만 보인다.
        var view = CarryView.Of(HeldItem.Assembled(
            new[] { Ingredient.BreadBase, Ingredient.Berry }));

        Assert.IsFalse(view.Empty);
        Assert.IsFalse(view.IsProduct);
        Assert.IsTrue(view.Assembled);
        Assert.AreEqual(2, view.Count);
        Assert.IsFalse(view.Equals(CarryView.Of(Ingredient.BreadBase)),
            "얹기 전과 후가 같은 값이면 조리대 표시가 갱신되지 않는다");
    }

    [Test]
    public void EqualityIgnoresNothingButTheShownFields()
    {
        var a = CarryView.Of(new HeldItem
        {
            IsProduct = true, Menu = MenuId.IcedLatte, Recipe = new[] { Ingredient.Bean },
        });
        var b = CarryView.Of(new HeldItem
        {
            IsProduct = true, Menu = MenuId.IcedLatte, Recipe = new[] { Ingredient.Ice },
        });

        // 레시피는 복제되지 않는다. 같은 메뉴면 NetworkVariable이 더티로 잡으면 안 된다.
        Assert.IsTrue(a.Equals(b));
    }
}
