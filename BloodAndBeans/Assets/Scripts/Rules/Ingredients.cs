/// Forest ingredients (design doc 7.1). Bean/BloodBean/Upgrade are heavy, Ice is light.
public enum Ingredient
{
    None = -1,
    Milk, Cream, Chocolate, Almond, Berry, Ice, BloodBean, UpgradePart,

    // Cafe staples, always stocked, never farmed (doc 7.1).
    Bean, BreadBase,
}

public static class Ingredients
{
    // ponytail: placeholder weights — doc 14장 #3 lists these as undecided.
    // Only the ordering (ice light, bean/upgrade heavy) is specified so far.
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
