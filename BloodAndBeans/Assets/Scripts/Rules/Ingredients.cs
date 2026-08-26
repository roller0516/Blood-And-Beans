/// 숲 재료 (기획서 7.1). 원두/블러드빈/업그레이드 부품은 무겁고, 얼음은 가볍다.
public enum Ingredient
{
    None = -1,
    Milk, Cream, Chocolate, Almond, Berry, Ice, BloodBean, UpgradePart,

    // 카페 상비 재료. 항상 재고가 있고 숲에서 캐지 않는다 (기획서 7.1).
    Bean, BreadBase,
}

public static class Ingredients
{
    // ponytail: 임시 무게값 — 기획서 14장 #3에서 미결정으로 남아 있다.
    // 지금까지 확정된 것은 순서(얼음이 가볍고 원두/업그레이드가 무겁다)뿐이다.
    static readonly float[] Weight =
    {
        1.0f, // Milk
        1.0f, // Cream
        0.8f, // Chocolate
        0.6f, // Almond
        0.7f, // Berry
        0.3f, // Ice
        2.5f, // BloodBean
        3.0f, // UpgradePart
        1.2f, // Bean
        1.0f, // BreadBase
    };

    public static float WeightOf(Ingredient i) =>
        i == Ingredient.None ? 0f : Weight[(int)i];

    /// 원두와 빵 베이스는 카페 상비 재료다 (doc 7.1) — 숲에서 캐지 않고, 팀 재고에도
    /// 쌓이지 않고, 인기 재료 추첨 대상도 아니다. 그 판정이 세 곳에 흩어져 있었다.
    public static bool IsStaple(Ingredient i) =>
        i == Ingredient.Bean || i == Ingredient.BreadBase;
}
