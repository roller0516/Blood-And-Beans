/// 적재 무게에 따른 이동 속도 (기획서 6.7). 씬이나 NetworkBehaviour 없이도 표를 기획서와
/// 대조할 수 있도록 PlayerInventory에서 분리했다.
///
/// 보간 대신 밴드 인덱스를 쓴다. 임대료 페널티는 적재 단계를 정확히 한 칸 낮추는데
/// (기획서 3.3 밤 항목), 밴드 폭이 제각각이라(0.5/0.3/0.2/0.3/0.3/0.4) 비율에 값을 더하는
/// 방식은 어떤 구간에서는 두 밴드를 건너뛰고 가벼울 때는 한 칸도 안 내려갔다.
public static class LoadBands
{
    static readonly float[] Speed = { 1.00f, 0.92f, 0.80f, 0.55f, 0.30f, 0.10f, 0.01f };

    public static int Count => Speed.Length;

    public static int BandOf(float loadRatio) =>
        loadRatio < 0.5f ? 0 :
        loadRatio < 0.8f ? 1 :
        loadRatio < 1.0f ? 2 :
        loadRatio < 1.3f ? 3 :
        loadRatio < 1.6f ? 4 :
        loadRatio < 2.0f ? 5 : 6;

    public static float SpeedOfBand(int band) =>
        Speed[band < 0 ? 0 : band >= Speed.Length ? Speed.Length - 1 : band];

    public static float SpeedMultiplier(float loadRatio) => SpeedOfBand(BandOf(loadRatio));

    /// 한 단계 나빠지되 마지막 밴드보다 더 내려가지는 않는다 (기획서 3.3 3단계).
    public static float SpeedMultiplierShifted(float loadRatio, bool shifted) =>
        SpeedOfBand(BandOf(loadRatio) + (shifted ? 1 : 0));
}
