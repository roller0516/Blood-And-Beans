using NUnit.Framework;

public class DayV5Tests
{
    [Test]
    public void BuffRenewalExpiresWithoutStackingOrAffectingOtherTeams()
    {
        var team = new TeamBuffs();
        var other = new TeamBuffs();
        Assert.IsTrue(team.Apply(TeamBuff.Move, 1));
        Assert.AreEqual(3, team.Remaining(TeamBuff.Move, 1));
        Assert.AreEqual(1, team.Remaining(TeamBuff.Move, 3));
        Assert.AreEqual(0, team.Remaining(TeamBuff.Move, 4));
        team.Apply(TeamBuff.Move, 2);
        team.Apply(TeamBuff.Move, 2);
        Assert.AreEqual(3, team.Remaining(TeamBuff.Move, 2));
        Assert.AreEqual(0, other.Remaining(TeamBuff.Move, 2));
        Assert.IsFalse(team.Apply((TeamBuff)99, 1));
        Assert.IsFalse(team.Apply(TeamBuff.Move, 0));
    }

    [Test]
    public void EmptyDishRemainsVisibleAndKeepsItsType()
    {
        var dish = CarryView.Of(HeldItem.Dish(true, true));
        Assert.IsFalse(dish.Empty);
        Assert.IsTrue(dish.HasDish);
        Assert.IsTrue(dish.DishIsPlate);
        Assert.IsTrue(dish.Dirty);
        Assert.IsFalse(dish.Equals(CarryView.Nothing));
    }
}
