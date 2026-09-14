/// 숲 구역과 박스 규칙 (기획서 6.3 · 6.3.1).
///
/// 밤마다의 배치(`MatchDirector.ScatterBoxesServer`)와 편집 도구(`ForestMapBuilder`)가 같은 표를 본다.
/// Mathf 대신 System.Math를 쓰는 이유는 BB.Rules가 UnityEngine을 참조하지 않기 때문이다.
public static class ForestRings
{
    public enum Zone { Outer, Middle, Core }

    /// 구역 경계. 중심까지의 거리를 숲 반지름으로 나눈 값이다.
    /// ponytail: 기획서 6.3에 구역 반경이 없다. 레벨 디자인이 치수를 정하면 맵 데이터로 옮긴다.
    public const float CoreRatio = 0.25f;
    public const float MidRatio = 0.55f;

    /// 매 밤 총 개수 범위 (기획서 6.3.1). 일차와 무관하다.
    /// 6.3.1의 팀 수 비례(3팀 ×1.5 · 4팀 ×2)는 사용자 결정으로 적용하지 않는다.
    public const int MinBoxes = 12;
    public const int MaxBoxes = 16;

    /// 구역 배분 퍼센트. 순서는 `Zone`과 같다 (기획서 6.3.1: 바깥 45 · 중간 35 · 중심 20).
    static readonly int[] ZoneShares = { 45, 35, 20 };

    /// [구역][일차-1] = (1등급, 2등급, 3등급) 퍼센트 (기획서 6.3.1 「자리 × 일차」).
    static readonly (int T1, int T2, int T3)[][] TierTable =
    {
        new[] { (100, 0, 0), (95, 5, 0), (90, 10, 0), (85, 15, 0), (80, 20, 0), (75, 25, 0), (70, 30, 0) },
        new[] { (70, 30, 0), (55, 45, 0), (40, 55, 5), (30, 60, 10), (25, 60, 15), (20, 60, 20), (15, 60, 25) },
        new[] { (30, 70, 0), (15, 75, 10), (10, 65, 25), (5, 55, 40), (0, 45, 55), (0, 35, 65), (0, 25, 75) },
    };

    public static Zone ZoneOf(float ratio) =>
        ratio < CoreRatio ? Zone.Core : ratio < MidRatio ? Zone.Middle : Zone.Outer;

    /// 구역과 일차의 등급 가중치. 표 밖의 일차는 가장 가까운 끝으로 붙인다.
    public static (int T1, int T2, int T3) Weights(Zone zone, int day)
    {
        var row = TierTable[(int)zone];
        return row[System.Math.Min(System.Math.Max(day, 1), row.Length) - 1];
    }

    /// 총 개수를 구역별로 나눈다. 내림한 뒤 남는 몫은 소수부가 큰 구역부터 준다 — 14개면 6 · 5 · 3.
    public static int[] Split(int total)
    {
        var counts = new int[ZoneShares.Length];
        var remainders = new int[ZoneShares.Length];
        var assigned = 0;
        for (var i = 0; i < counts.Length; i++)
        {
            counts[i] = total * ZoneShares[i] / 100;
            remainders[i] = total * ZoneShares[i] % 100;
            assigned += counts[i];
        }

        // 남는 몫은 구역 수보다 적으므로 한 구역이 두 번 받지 않는다.
        for (; assigned < total; assigned++)
        {
            var best = 0;
            for (var i = 1; i < counts.Length; i++)
                if (remainders[i] > remainders[best]) best = i;

            counts[best]++;
            remainders[best] = -1;
        }

        return counts;
    }
}
