using System.Collections.Generic;
using NUnit.Framework;

/// 상자 칸 규칙 — 종류 기준 5칸, 스택, 초과 시 분할, 1초 간격 순차 공개.
public class LootSlotTests
{
    [Test]
    public void SameTypeStacksIntoOneSlot()
    {
        var boxes = LootSlots.Pack(new[]
        {
            Ingredient.Milk, Ingredient.Ice, Ingredient.Milk, Ingredient.Milk,
        });

        Assert.AreEqual(1, boxes.Count);
        Assert.AreEqual(2, boxes[0].Count, "종류가 둘이면 칸도 둘이다");
        Assert.AreEqual(Ingredient.Milk, boxes[0][0].Item);
        Assert.AreEqual(3, boxes[0][0].Count);
        Assert.AreEqual(Ingredient.Ice, boxes[0][1].Item);
        Assert.AreEqual(1, boxes[0][1].Count);
    }

    [Test]
    public void MoreThanFiveTypesSplitsIntoSeveralBoxes()
    {
        // 12종류를 버리면 5 / 5 / 2로 임시 상자 셋이 된다.
        var dropped = new List<Ingredient>();
        var types = new[]
        {
            Ingredient.Milk, Ingredient.Cream, Ingredient.Chocolate, Ingredient.Almond,
            Ingredient.Berry, Ingredient.Ice, Ingredient.BloodBean, Ingredient.UpgradePart,
            Ingredient.Bean, Ingredient.BreadBase,
        };
        foreach (var t in types) dropped.Add(t);

        var boxes = LootSlots.Pack(dropped);

        Assert.AreEqual(2, boxes.Count);
        Assert.AreEqual(5, boxes[0].Count);
        Assert.AreEqual(5, boxes[1].Count);
        foreach (var box in boxes)
            Assert.LessOrEqual(box.Count, LootSlots.MaxTypes, "상자 하나가 5종류를 넘었다");
    }

    [Test]
    public void NothingDroppedMakesNoBox()
    {
        Assert.AreEqual(0, LootSlots.Pack(new Ingredient[0]).Count);
        Assert.AreEqual(0, LootSlots.Pack(new[] { Ingredient.None }).Count);
    }

    [Test]
    public void SlotsRevealOneEverySecondWithNoSkip()
    {
        // 개봉 직후에는 전부 가려져 있고, 5칸이면 5초에 다 드러난다.
        Assert.AreEqual(0, LootSlots.RevealedCount(100d, 100d, 1f, 5));
        Assert.AreEqual(0, LootSlots.RevealedCount(100.9d, 100d, 1f, 5));
        Assert.AreEqual(1, LootSlots.RevealedCount(101d, 100d, 1f, 5));
        Assert.AreEqual(4, LootSlots.RevealedCount(104.5d, 100d, 1f, 5));
        Assert.AreEqual(5, LootSlots.RevealedCount(105d, 100d, 1f, 5));
    }

    [Test]
    public void RevealNeverExceedsTheSlotCount()
    {
        Assert.AreEqual(3, LootSlots.RevealedCount(1000d, 100d, 1f, 3));
        Assert.AreEqual(0, LootSlots.RevealedCount(99d, 100d, 1f, 5), "개봉 전은 음수가 아니라 0이다");
    }

    // --- 등급별 슬롯 수 (기획서 6.5.2) ---

    [Test]
    public void BoxGradeDecidesHowManySlots()
    {
        LootSlots.SlotRangeFor(1, out var min, out var max);
        Assert.AreEqual(2, min, "1등급 하한");
        Assert.AreEqual(3, max, "1등급 상한");

        LootSlots.SlotRangeFor(2, out min, out max);
        Assert.AreEqual(3, min, "2등급 하한");
        Assert.AreEqual(4, max, "2등급 상한");

        LootSlots.SlotRangeFor(3, out min, out max);
        Assert.AreEqual(4, min, "3등급 하한");
        Assert.AreEqual(5, max, "3등급 상한");
    }

    [Test]
    public void EveryGradeIsARangeNotAFixedCount()
    {
        // 고정 값으로 되돌아가면 이 테스트가 먼저 깨진다.
        for (var tier = 1; tier <= 3; tier++)
        {
            LootSlots.SlotRangeFor(tier, out var min, out var max);
            Assert.Less(min, max, $"{tier}등급이 고정 칸 수가 됐다");
        }
    }

    [Test]
    public void SlotCountNeverExceedsTheTypeLimit()
    {
        LootSlots.SlotRangeFor(9, out var min, out var max);
        Assert.LessOrEqual(max, LootSlots.MaxTypes, "칸 제한은 종류 기준 5개다");
        Assert.LessOrEqual(min, max);

        LootSlots.SlotRangeFor(0, out min, out max);
        Assert.AreEqual(2, min, "등급 0은 1등급으로 본다");
    }

    // --- 귀환 정산 (기획서 5장) ---

    [Test]
    public void MissingTheReturnPointLosesTheGivenShare()
    {
        var bag = new List<Ingredient>
        {
            Ingredient.Ice, Ingredient.Milk, Ingredient.Berry, Ingredient.Cream,
        };

        var lost = RandomLoss.TakeShare(bag, 0.25f, new System.Random(3));

        Assert.AreEqual(1, lost.Count);
        Assert.AreEqual(3, bag.Count);
    }

    [Test]
    public void ASingleItemIsNotSafeFromAPartialLoss()
    {
        var bag = new List<Ingredient> { Ingredient.BloodBean };

        Assert.AreEqual(1, RandomLoss.TakeShare(bag, 0.1f, new System.Random(1)).Count,
            "올림이라 한 개도 잃는다");
        Assert.AreEqual(0, bag.Count);
    }

    [Test]
    public void FullShareTakesEverything()
    {
        var bag = new List<Ingredient> { Ingredient.Ice, Ingredient.Milk, Ingredient.Berry };

        Assert.AreEqual(3, RandomLoss.TakeShare(bag, 1f, new System.Random(1)).Count);
        Assert.AreEqual(0, bag.Count);
    }
}
