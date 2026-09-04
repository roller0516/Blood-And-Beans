/// 적재 무게에 따른 이동 속도 (기획서 6.7). 씬이나 NetworkBehaviour 없이도 표를 기획서와
/// 대조할 수 있도록 PlayerInventory에서 분리했다.
///
/// 보간 대신 밴드 인덱스를 쓴다. 임대료 페널티는 적재 단계를 정확히 한 칸 낮추는데
/// (기획서 3.3 밤 항목), 밴드 폭이 제각각이라(0.5/0.3/0.2/0.3/0.3/0.4) 비율에 값을 더하는
/// 방식은 어떤 구간에서는 두 밴드를 건너뛰고 가벼울 때는 한 칸도 안 내려갔다.
public static class LoadBands
{
    static readonly float[] Speed = { 1.00f, 0.92f, 0.80f, 0.55f, 0.30f, 0.10f, 0.01f };

    /// 겉보기와 견제가 갈리는 선 (기획서 6.6: "적재 80% 이상인 상대에게 대시" / "적재
    /// 80%를 넘긴 캐릭터는 겉보기에도 표시된다").
    ///
    /// 두 규칙이 같은 수치를 쓰는 것이 요점이다. 갈라지면 부풀어 보이는데 아무것도 흘리지
    /// 않거나, 멀쩡해 보이는 상대가 재료를 쏟는다 — 어느 쪽이든 견제 판단의 근거가 거짓이 된다.
    public const float OverloadRatio = 0.8f;

    /// 무게가 화면을 흔들기 시작하는 선 (기획서 6.7: "100%를 넘으면 화면 흔들림이 붙는다").
    public const float ShakeRatio = 1.0f;

    /// 대시를 더 쓸 수 없게 되는 선 (기획서 6.6: "대시는 가방의 무게가 70%가 초과할 경우,
    /// 사용이 제한된다").
    ///
    /// 겉보기·낙하가 걸리는 80%(`OverloadRatio`)보다 낮다. 그래서 "대시를 못 쓰는데 아직
    /// 안 부푼" 구간이 10%p 생기는데, 그것이 기획서가 의도한 순서다 — 견제 수단을 먼저
    /// 잃고 그다음에 표적이 된다.
    public const float DashBlockRatio = 0.7f;

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
