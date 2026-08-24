using NUnit.Framework;

/// 기획서 6.5.1 (개봉·담기 홀드) / 6.6 (대시 중단) / 6.7 (무게 → 이동속도).
public class NightRuleTests
{
    [Test]
    public void MissingReturnLosesRandomHalfByItemCount()
    {
        var bag = new System.Collections.Generic.List<Ingredient>
        {
            Ingredient.Ice, Ingredient.BloodBean, Ingredient.Milk,
            Ingredient.Berry, Ingredient.Chocolate,
        };

        var lost = RandomLoss.TakeHalf(bag, new System.Random(7));

        Assert.That(lost.Count, Is.EqualTo(3));
        Assert.That(bag.Count, Is.EqualTo(2));
        Assert.That(lost, Is.Not.EquivalentTo(new[]
        {
            Ingredient.Ice, Ingredient.Milk, Ingredient.Berry,
        }));
    }

    // --- 6.5.1 홀드는 서버가 잰다 ---

    [Test]
    public void AHoldThatWasNotPaidForDoesNotComplete()
    {
        var h = new HoldTimer();
        h.Begin(1, 100d);
        Assert.IsFalse(h.TryConsume(1, 100.5d, 1f), "1초짜리 홀드가 0.5초에 끝나면 안 된다");
        Assert.IsTrue(h.TryConsume(1, 101d, 1f));
    }

    [Test]
    public void AHeldKeyYieldsOncePerInterval()
    {
        // 담기는 개당 짧은 간격으로 연속된다 (6.5.1).
        //
        // 간격이 0.25인 이유는 이 테스트가 반복 규칙만 보기 위해서다. 0.2f를 double로
        // 넓히면 0.2d보다 아주 조금 커서 "정확히 경계" 비교가 뒤집힌다. 서버 시간이
        // 경계에 정확히 떨어지는 일은 없으므로 실제 동작 문제는 아니고, 여기서 검증할
        // 것은 float 정밀도가 아니라 "완료 시점부터 다시 센다"이다.
        var h = new HoldTimer();
        h.Begin(1, 0d);
        Assert.IsTrue(h.TryConsume(1, 0.25d, 0.25f));
        Assert.IsFalse(h.TryConsume(1, 0.4d, 0.25f), "완료 시점부터 다시 센다");
        Assert.IsTrue(h.TryConsume(1, 0.5d, 0.25f));
    }

    [Test]
    public void RepeatBeginDoesNotRewindProgress()
    {
        // 이 클래스가 존재하는 이유. Begin을 연타해 진행도를 되감을 수 있으면
        // 서버가 시간을 재는 의미가 없다.
        var h = new HoldTimer();
        h.Begin(1, 200d);
        h.Begin(1, 200.9d);
        Assert.IsTrue(h.TryConsume(1, 201d, 1f));
    }

    [Test]
    public void CancelClearsAndConsumingWithoutBeginFails()
    {
        var h = new HoldTimer();
        Assert.IsFalse(h.TryConsume(9, 0d, 0.1f), "시작하지 않은 홀드는 완료될 수 없다");

        h.Begin(9, 0d);
        h.Cancel(9);
        Assert.IsFalse(h.Holding(9));
        Assert.IsFalse(h.TryConsume(9, 100d, 0.1f));
    }

    [Test]
    public void HoldsAreIsolatedPerClient()
    {
        // 한 박스에 두 명이 붙어도 서로의 진행도를 쓰지 못한다.
        var h = new HoldTimer();
        h.Begin(1, 0d);
        Assert.IsFalse(h.Holding(2));
        Assert.IsFalse(h.TryConsume(2, 10d, 1f));
        Assert.IsTrue(h.TryConsume(1, 10d, 1f));
    }

    [Test]
    public void DashKeepsHalfOfTheProgress()
    {
        // 6.6: 개봉 중단, 진행도 50% 유지.
        var h = new HoldTimer();
        h.Begin(1, 0d);
        h.Halve(1, 0.8d);
        Assert.AreEqual(0.4f, h.Elapsed(1, 0.8d), 0.001f);
    }

    [Test]
    public void HalveOnSomeoneNotHoldingIsANoOp()
    {
        var h = new HoldTimer();
        h.Halve(1, 5d);
        Assert.IsFalse(h.Holding(1));
    }

    [Test]
    public void CancelAllClearsEveryone()
    {
        // 밤이 끝나면 진행 중이던 홀드는 전부 사라져야 한다.
        var h = new HoldTimer();
        h.Begin(1, 0d);
        h.Begin(2, 0d);
        h.CancelAll();
        Assert.IsFalse(h.Holding(1));
        Assert.IsFalse(h.Holding(2));
    }

    // --- 6.7 무게 → 이동속도 ---

    [Test]
    public void SpeedTableMatchesTheDoc()
    {
        Assert.AreEqual(1.00f, LoadBands.SpeedMultiplier(0.00f), 0.0001f);
        Assert.AreEqual(1.00f, LoadBands.SpeedMultiplier(0.49f), 0.0001f);
        Assert.AreEqual(0.92f, LoadBands.SpeedMultiplier(0.50f), 0.0001f);
        Assert.AreEqual(0.92f, LoadBands.SpeedMultiplier(0.79f), 0.0001f);
        Assert.AreEqual(0.80f, LoadBands.SpeedMultiplier(0.80f), 0.0001f);
        Assert.AreEqual(0.55f, LoadBands.SpeedMultiplier(1.00f), 0.0001f);
        Assert.AreEqual(0.30f, LoadBands.SpeedMultiplier(1.30f), 0.0001f);
        Assert.AreEqual(0.10f, LoadBands.SpeedMultiplier(1.60f), 0.0001f);
        Assert.AreEqual(0.01f, LoadBands.SpeedMultiplier(2.00f), 0.0001f);
        Assert.AreEqual(0.01f, LoadBands.SpeedMultiplier(9.99f), 0.0001f, "200% 이상은 사실상 정지");
    }

    [Test]
    public void SpeedNeverIncreasesWithLoad()
    {
        var previous = float.MaxValue;
        for (var ratio = 0f; ratio <= 2.5f; ratio += 0.01f)
        {
            var speed = LoadBands.SpeedMultiplier(ratio);
            Assert.LessOrEqual(speed, previous + 0.0001f, $"ratio {ratio:0.00}에서 속도가 올라갔다");
            previous = speed;
        }
    }

    [Test]
    public void ThePenaltyShiftsExactlyOneBand()
    {
        // 3.3 tier 3. 구간 폭이 제각각이라(0.5/0.3/0.2/0.3/0.3/0.4) 비율에 상수를 더하는
        // 방식은 어떤 곳에서는 두 칸을 건너뛰고 가벼울 때는 한 칸도 못 간다.
        for (var ratio = 0f; ratio <= 2.5f; ratio += 0.05f)
        {
            var band = LoadBands.BandOf(ratio);
            var next = band + 1 < LoadBands.Count ? band + 1 : band;
            Assert.AreEqual(LoadBands.SpeedOfBand(next),
                LoadBands.SpeedMultiplierShifted(ratio, true), 0.0001f,
                $"ratio {ratio:0.00}에서 정확히 한 칸이 아니다");
        }

        Assert.AreEqual(0.92f, LoadBands.SpeedMultiplierShifted(0.0f, true), 0.0001f);
        Assert.AreEqual(1.00f, LoadBands.SpeedMultiplierShifted(0.0f, false), 0.0001f);
        Assert.AreEqual(0.80f, LoadBands.SpeedMultiplierShifted(0.5f, true), 0.0001f);
    }

    [Test]
    public void TheShiftCannotFallOffTheTable()
    {
        Assert.AreEqual(0.01f, LoadBands.SpeedMultiplierShifted(5f, true), 0.0001f);
    }
}
