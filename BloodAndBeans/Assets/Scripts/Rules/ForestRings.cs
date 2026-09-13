/// 숲 링별 등급 가중치 (기획서 6.3: 바깥 1등급 위주 · 중간 2등급 · 중심 3등급).
///
/// 편집 시점의 배치(`ForestMapBuilder`)와 밤마다의 재배치(`MatchDirector.ScatterBoxesServer`)가
/// 같은 표를 본다. 두 벌로 두면 상자가 옮겨 다닐 때 자리와 등급이 어긋난다.
public static class ForestRings
{
    /// 링 경계. 중심까지의 거리를 숲 반지름으로 나눈 값이다.
    public const float CoreRatio = 0.25f;
    public const float MidRatio = 0.55f;

    /// 0으로 잘라내지 않고 꼬리를 남긴 이유는 매 밤 리롤(6.3)이 의미를 가지려면 링 안에서도
    /// 뽑기가 흔들려야 하기 때문이다.
    public static (int T1, int T2, int T3) WeightsFor(float ratio) =>
        ratio < CoreRatio ? (0, 2, 8)
        : ratio < MidRatio ? (2, 7, 1)
        : (8, 2, 0);
}
