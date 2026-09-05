using System.Collections.Generic;

/// 일차별 숲 리젠 테이블 (기획서 10장).
///
/// 기획서가 정한 것은 방향 하나다 — "초반은 흔한 재료 위주, 후반은 희귀 재료·업그레이드
/// 재료·블러드 빈 비중이 오른다". 그래서 이 표가 정하는 것도 하나뿐이다: **그날 밤
/// 3등급 상자가 중심부 보상으로 채우는 칸 수.**
///
/// 흔한 재료 풀 자체는 일차가 아니라 *맵*이 정한다 (10장 첫 줄: "맵마다 리젠되는 재료
/// 타입이 정해져 있다"). 일차로 흔한 재료를 잠그면 만들 수 있는 메뉴가 같이 줄어
/// 초반의 낮이 통째로 빈약해진다 — 기획서가 요구한 것은 그게 아니다.
///
/// 등급 분포는 여기서 건드리지 않는다. 어느 등급이 잘 나오는가는 자리가 정하고
/// (기획서 6.3, `ItemBox.tierWeights`), 여기는 그 등급이 무엇을 담는가만 정한다.
public static class RegenTable
{
    /// 맵 데이터가 아직 없을 때 쓰는 키. `MatchDirector.MapId`의 기본값과 같다 —
    /// 씬에 맵 ID를 안 채워도(또는 등록 안 된 ID를 채워도) 조용히 이 풀로 떨어진다.
    public const string DefaultMapId = "default";

    /// 맵별 흔한 재료. 맵마다 다른 리젠 타입(기획서 10장 첫 줄: "맵마다 리젠되는 재료
    /// 타입이 정해져 있다")을 여기 한 줄씩 추가한다 — 그게 전부다. 조회하는 쪽
    /// (`PoolFor`)과 심는 쪽(`MatchDirector.MapId`)은 이미 맵 ID로 짜여 있어서 코드를
    /// 더 안 고쳐도 된다.
    ///
    /// 기본값(`DefaultMapId`)의 재료는 기획서 7.1 「숲에서 캐는 재료」에서 중심부 보상 둘
    /// (블러드 빈 · 업그레이드 재료)을 뺀 나머지다.
    ///
    /// ponytail: 지금은 기본 맵 한 벌뿐이다. 실제 맵이 정해지면 그 맵의 ID로 항목을
    /// 추가한다 — `DT_Regen`이 생기면 이 표 자체를 데이터 에셋으로 옮긴다.
    static readonly Dictionary<string, Ingredient[]> ByMap = new()
    {
        [DefaultMapId] = new[]
        {
            Ingredient.Milk, Ingredient.Cream, Ingredient.Chocolate,
            Ingredient.Almond, Ingredient.Berry, Ingredient.Ice,
        },
    };

    /// 일차별 중심부 보상 칸 수. 3등급 상자 하나가 이만큼을 블러드 빈·업그레이드 재료로
    /// 채우고 나머지를 흔한 재료로 메운다 (기획서 6.5.2: 3등급은 4~5칸).
    ///
    /// ponytail: 기획서 10장에 표가 없고 14장에도 항목이 없다. 확정된 것은 "후반에
    /// 비중이 오른다"는 방향뿐이라 그 방향만 지킨 임시 표다. `DT_Regen`이 생기면 옮긴다.
    static readonly int[] RareSlotsByDay = { 1, 1, 2, 2, 3, 3, 3 };

    /// 표를 넘는 일차는 마지막 값으로 고정한다. `Rent.Due`와 같은 처리이며 이유도 같다 —
    /// 기획서가 7일까지만 정했다.
    ///
    /// `Mathf` 대신 `System.Math`를 쓰는 이유는 BB.Rules가 UnityEngine을 참조하지 않기
    /// 때문이다.
    public static int RareSlots(int day) =>
        RareSlotsByDay[System.Math.Min(System.Math.Max(day, 1), RareSlotsByDay.Length) - 1];

    /// 그 맵의 그날 밤 숲이 내놓는 것. 카페 상비 재료(원두·빵 베이스)는 여기 없다 —
    /// 숲에서 캐지 않고 인기 재료 추첨 대상도 아니다 (기획서 7.1, 5.6.1).
    ///
    /// `mapId`가 `ByMap`에 없으면(맵 데이터가 아직 없거나 오타) 기본 풀로 떨어진다.
    /// 여기서 예외를 던지면 등록 안 된 맵마다 그 밤의 파밍이 통째로 멈춘다 — 자리를
    /// 비우는 것보다는 기본 재료라도 내주는 쪽이 낫다.
    public static IReadOnlyList<Ingredient> PoolFor(string mapId, int day) =>
        ByMap.TryGetValue(mapId, out var pool) ? pool : ByMap[DefaultMapId];
}
