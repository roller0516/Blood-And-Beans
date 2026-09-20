/// 숲 재료 (기획서 7.1). 원두/블러드빈/업그레이드 부품은 무겁고, 얼음은 가볍다.
public enum Ingredient
{
    None = -1,
    Milk, Cream, Chocolate, Almond, Berry, Ice, BloodBean, UpgradePart,

    // 카페 상비 재료. 항상 재고가 있고 숲에서 캐지 않는다 (기획서 7.1).
    Bean, BreadBase,
    // 보석 6종 (기획서 8.2). 스프라이트 표·프리팹이 정수값으로 저장하므로 값을 옮기지 않는다.
    WindGem = 10, ScalesGem = 11, EngravingGem = 12, FoamGem = 13, EmberGem = 14, TeaGem = 15,
}

/// 재료 등급. 순서가 곧 희귀도이므로 색·정렬에 인덱스로 쓸 수 있다.
public enum IngredientRarity
{
    Common,
    Rare,
}

public static class Ingredients
{
    // 기획서 6.7: 얼음 0.5 · 일반 재료 1 · 블러드 빈과 보석 3. 표는 `BalanceData.IngredientWeight`에 있다.
    static float[] Weight => Balance.Current.IngredientWeight;

    public static float WeightOf(Ingredient i) =>
        i == Ingredient.None ? 0f : Gems.IsGem(i) ? Weight[(int)Ingredient.UpgradePart] : Weight[(int)i];

    /// 재료 등급 (기획서 6.5.2). 상자 등급이 무엇을 담는지가 곧 재료 등급이다 — 흔한
    /// 재료는 어느 등급 상자에서나 나오고, 업그레이드 재료·블러드 빈은 3등급 상자에만
    /// 들어간다 (6.3). 상자마다 뽑는 풀은 `ItemBox`가 Inspector로 들고 있지만, 그것은
    /// "이 자리에서 무엇이 나오는가"고 이쪽은 "이 재료가 무엇인가"다.
    public static IngredientRarity RarityOf(Ingredient i) =>
        i == Ingredient.BloodBean || Gems.IsGem(i)
            ? IngredientRarity.Rare
            : IngredientRarity.Common;

    /// 원두와 빵 베이스는 카페 상비 재료다 (doc 7.1) — 숲에서 캐지 않고, 팀 재고에도
    /// 쌓이지 않고, 인기 재료 추첨 대상도 아니다. 그 판정이 세 곳에 흩어져 있었다.
    public static bool IsStaple(Ingredient i) =>
        i == Ingredient.Bean || i == Ingredient.BreadBase;
}
