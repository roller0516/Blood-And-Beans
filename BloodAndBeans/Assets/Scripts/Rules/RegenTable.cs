using System.Collections.Generic;

/// 일차별 숲 리젠 테이블 (기획서 10장).
///
/// 보석·블러드 빈 확률은 여기가 아니라 `Gems`에 있다 (기획서 6.5.2).
///
/// 흔한 재료는 *맵*이 종류를 정하고(10장), *일차*가 가중치를 정한다 (6.3.2).
/// 가중치 0인 날에는 그 재료가 리젠되지 않는다 — 초콜렛은 3일차, 베리는 4일차부터다.
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
    public const string BerryGroveMapId = "berry-grove";

    /// 그 맵의 그날 밤 숲이 내놓는 것. 카페 상비 재료(원두·빵 베이스)는 여기 없다 —
    /// 숲에서 캐지 않고 인기 재료 추첨 대상도 아니다 (기획서 7.1, 5.6.1).
    ///
    /// `mapId`가 `ByMap`에 없으면(맵 데이터가 아직 없거나 오타) 기본 풀로 떨어진다.
    /// 여기서 예외를 던지면 등록 안 된 맵마다 그 밤의 파밍이 통째로 멈춘다 — 자리를
    /// 비우는 것보다는 기본 재료라도 내주는 쪽이 낫다.
    // 표는 `BalanceData.RegenWeights`(6.3.2)와 `BalanceData.RegenMaps`(10장)에 있다.

    /// 그날의 가중치. 표에 없는 재료는 0이다.
    ///
    /// 표가 6줄이라 선형 탐색으로 둔다. 예전 `Dictionary` 조회와 비용이 비슷하고,
    /// 줄 순서가 데이터 파일 순서와 같아 대조하기 쉽다.
    public static int WeightOf(Ingredient item, int day)
    {
        var rows = Balance.Current.RegenWeights;
        for (var i = 0; i < rows.Length; i++)
            if (rows[i].Item == item) return Balance.ByDay(rows[i].ByDay, day);
        return 0;
    }

    /// 맵의 재료 중 그날 가중치가 있는 것. 페이즈 경계에서만 불리므로 목록을 새로 만든다.
    public static IReadOnlyList<Ingredient> PoolFor(string mapId, int day)
    {
        var map = PoolOf(mapId);
        var today = new List<Ingredient>(map.Length);
        foreach (var item in map)
            if (WeightOf(item, day) > 0) today.Add(item);
        return today;
    }

    /// 등록 안 된 맵은 기본 풀로 떨어진다. 기본 풀마저 없으면 빈 목록이다 — 예외를
    /// 던지면 데이터 오타 하나로 그 밤의 파밍이 통째로 멈춘다.
    static Ingredient[] PoolOf(string mapId)
    {
        var maps = Balance.Current.RegenMaps;
        Ingredient[] fallback = null;
        for (var i = 0; i < maps.Length; i++)
        {
            if (maps[i].MapId == mapId) return maps[i].Pool;
            if (maps[i].MapId == DefaultMapId) fallback = maps[i].Pool;
        }
        return fallback ?? System.Array.Empty<Ingredient>();
    }
}
