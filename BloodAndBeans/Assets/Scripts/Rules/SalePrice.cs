using System.Collections.Generic;

/// 완성 게이지 판정 (기획서 5.2). Burnt는 탄 것을 그대로 판 경우다 (5.3 "판다").
public enum Gauge { Perfect, Good, Miss, Burnt }

/// 원두 등급 대체 (7.2). 디저트는 이 값을 아예 무시한다 (5.6.2).
public enum BeanGrade { Normal, Blood }

/// 최종 판매가, 기획서 5.6.2:
///   가격 = 기본가 x 게이지 x 원두등급 x (1 + 인기재료 보너스 합)
/// 상태가 없고 UnityEngine에 의존하지 않아 SalePriceSelfCheck가 어디서든 돌릴 수 있다.
public static class SalePrice
{
    public const float PopularBonus = 0.30f;

    // 기획서 1.4 · 7.2: 블러드 빈을 쓰면 커피 판매가 ×3.
    public const float BloodBeanMultiplier = 3f;

    public static float GaugeMultiplier(Gauge g) => g switch
    {
        Gauge.Perfect => 1.3f,
        Gauge.Good => 1.0f,
        Gauge.Miss => 0.7f,
        _ => 0.2f,   // 기획서 5.2: 탐 ×0.2
    };

    /// 메뉴에 인기 재료가 몇 개 들어 있는지 센다. 메뉴는 재료 집합이라 중복이
    /// 없으므로 단순 순회로 충분하다.
    public static int PopularCount(IReadOnlyList<Ingredient> menu, IReadOnlyList<Ingredient> popular)
    {
        if (menu == null || popular == null) return 0;
        var n = 0;
        for (var i = 0; i < popular.Count; i++)
            for (var j = 0; j < menu.Count; j++)
                if (menu[j] == popular[i]) { n++; break; }
        return n;
    }

    /// 핵심 계산. basePrice는 메뉴 표에서 오고, menu에는 숲 재료만 들어간다
    /// (원두/빵 베이스는 항상 상비다, 7.1).
    public static int Calculate(
        int basePrice,
        Gauge gauge,
        BeanGrade grade,
        bool isDessert,
        IReadOnlyList<Ingredient> menu,
        IReadOnlyList<Ingredient> popular)
    {
        // 보너스는 곱하지 않고 합산해서 한 번만 적용한다 (5.6.2).
        // ponytail: 기획서 14장 #15 — 블러드빈/업그레이드 부품이 인기 재료가 될 수
        // 있는지 미결정이다. 여기서는 그대로 센다. 안 된다면 Forecast에서 걸러라.
        var bonus = 1f + PopularCount(menu, popular) * PopularBonus;
        var bean = (!isDessert && grade == BeanGrade.Blood) ? BloodBeanMultiplier : 1f;

        var raw = basePrice * GaugeMultiplier(gauge) * bean * bonus;
        return (int)System.Math.Round(raw, System.MidpointRounding.AwayFromZero);
    }
}
