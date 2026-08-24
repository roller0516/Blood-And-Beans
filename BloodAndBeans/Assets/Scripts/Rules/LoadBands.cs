/// Carry weight to movement speed (doc 6.7). Lifted out of PlayerInventory so the table
/// can be checked against the design doc without a scene or a NetworkBehaviour.
///
/// Bands are indexed rather than interpolated: a rent penalty moves the load exactly one
/// step (doc 3.3, night column), and the band widths differ (0.5/0.3/0.2/0.3/0.3/0.4), so
/// offsetting the ratio instead skipped two bands in places and none at all when light.
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

    /// One step worse, and no worse than the last band (doc 3.3 tier 3).
    public static float SpeedMultiplierShifted(float loadRatio, bool shifted) =>
        SpeedOfBand(BandOf(loadRatio) + (shifted ? 1 : 0));
}
