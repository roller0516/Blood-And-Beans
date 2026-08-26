using UnityEngine;

/// 소유자가 틱마다 예측한 위치를 담는 고정 크기 링 버퍼.
///
/// 서버 상태는 자기가 만들어진 틱을 달고 오므로, 화해는 "그 틱에 내가 예측했던 위치"를
/// 찾아야 한다(PlayerPrediction). 틱은 계속 늘고 오래된 것은 쓸모가 없어 링으로 둔다.
/// 슬롯에는 위치와 함께 틱 번호도 넣는다. 그래야 찾는 틱이 이미 덮여 밀려났을 때
/// 엉뚱한 위치를 돌려주는 대신 실패로 답할 수 있다.
public class PredictionHistory
{
    /// 빈 슬롯을 나타내는 틱. 실제 틱으로 나올 수 없는 값이어야 한다.
    const int EmptyTick = int.MinValue;

    readonly Vector3[] positions;
    readonly int[] ticks;

    public PredictionHistory(int capacity)
    {
        var size = Mathf.Max(1, capacity);
        positions = new Vector3[size];
        ticks = new int[size];
        Clear();
    }

    public int Capacity => positions.Length;

    public void Record(int tick, Vector3 position)
    {
        if (tick == EmptyTick) return;

        var i = Index(tick);
        ticks[i] = tick;
        positions[i] = position;
    }

    public bool TryGet(int tick, out Vector3 position)
    {
        var i = Index(tick);
        position = positions[i];
        return tick != EmptyTick && ticks[i] == tick;
    }

    /// 순간이동한 뒤에는 이전 궤적의 예측이 전부 무의미하다. 남겨 두면 맵을 가로지르는
    /// 가짜 오차가 나온다.
    public void Clear()
    {
        for (var i = 0; i < ticks.Length; i++) ticks[i] = EmptyTick;
    }

    /// 틱은 음수가 될 수 있다. 부호 있는 나머지는 음수 인덱스를 만들어 터진다.
    int Index(int tick) => (int)((uint)tick % (uint)positions.Length);
}
