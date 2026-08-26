using NUnit.Framework;
using UnityEngine;

/// 소유자 예측 화해는 "서버가 말한 틱에 내가 예측했던 위치"를 찾아야 성립한다
/// (PlayerPrediction). 그 조회가 틀리면 화해는 지연 거리를 오차로 착각해 매 프레임
/// 엉뚱한 곳으로 위치를 당긴다. 링 버퍼의 계약만 여기서 확인한다.
public class PredictionHistoryTests
{
    [Test]
    public void RecordedTickComesBack()
    {
        var history = new PredictionHistory(8);
        history.Record(100, new Vector3(1f, 0f, 2f));

        Assert.IsTrue(history.TryGet(100, out var found));
        Assert.AreEqual(new Vector3(1f, 0f, 2f), found);
    }

    [Test]
    public void UnknownTickFails()
    {
        var history = new PredictionHistory(8);
        history.Record(100, Vector3.one);

        Assert.IsFalse(history.TryGet(101, out _), "기록한 적 없는 틱은 실패해야 한다");
    }

    [Test]
    public void OverwrittenTickFailsInsteadOfReturningTheNewer()
    {
        // 용량 8이면 틱 100과 108은 같은 슬롯을 쓴다. 밀려난 쪽을 물으면 새 위치를
        // 그 틱의 예측인 것처럼 돌려주면 안 된다.
        var history = new PredictionHistory(8);
        history.Record(100, Vector3.zero);
        history.Record(108, new Vector3(9f, 0f, 9f));

        Assert.IsTrue(history.TryGet(108, out var newer));
        Assert.AreEqual(new Vector3(9f, 0f, 9f), newer);
        Assert.IsFalse(history.TryGet(100, out _), "덮인 틱은 실패로 답해야 한다");
    }

    [Test]
    public void NegativeTicksDoNotThrowAndStaySeparate()
    {
        // 부호 있는 나머지를 쓰면 음수 틱이 음수 인덱스가 되어 터진다.
        var history = new PredictionHistory(8);
        history.Record(-3, new Vector3(0f, 0f, -3f));

        Assert.IsTrue(history.TryGet(-3, out var found));
        Assert.AreEqual(new Vector3(0f, 0f, -3f), found);
    }

    [Test]
    public void ClearDropsEverything()
    {
        // 순간이동 뒤에 이전 궤적이 남아 있으면 맵을 가로지르는 가짜 오차가 나온다.
        var history = new PredictionHistory(8);
        history.Record(100, Vector3.one);
        history.Clear();

        Assert.IsFalse(history.TryGet(100, out _));
    }

    [Test]
    public void EmptySentinelTickIsNeverReportedAsFound()
    {
        // 빈 슬롯 표식(int.MinValue)을 그대로 조회했을 때 "찾았다"가 나오면
        // 0,0,0을 그 틱의 예측으로 믿고 원점으로 끌려간다.
        var history = new PredictionHistory(8);
        Assert.IsFalse(history.TryGet(int.MinValue, out _));
    }

    [Test]
    public void CapacityIsAtLeastOne()
    {
        Assert.AreEqual(1, new PredictionHistory(0).Capacity);
        Assert.AreEqual(1, new PredictionHistory(-5).Capacity);
    }
}
