/// 효과음 리스트의 번호. 클립 참조는 클라이언트 SoundManager 프리팹에 둔다.
public enum SfxCue
{
    None = 0,
    IngredientLiquid = 101, IngredientHard = 102, IngredientSoft = 103,
    GaugeDing = 4, GaugeTick = 5, Perfect = 6, Good = 7, Miss = 8,
    Sale = 10, Coin = 11, Misdelivery = 12, Spoiled = 14, HandOff = 16, PartialSale = 22, BurntSale = 23,
    GemExpire = 26, Bump = 27, BloodBean = 33, Gem = 34, Dash = 37,
    BloodBeanPour = 58, NotAllowed = 62,
}

public static class IngredientSfx
{
    public static SfxCue Of(Ingredient item) => item switch
    {
        Ingredient.Milk or Ingredient.Chocolate => SfxCue.IngredientLiquid,
        Ingredient.Bean or Ingredient.Ice or Ingredient.Almond or Ingredient.Berry => SfxCue.IngredientHard,
        Ingredient.BreadBase or Ingredient.Cream => SfxCue.IngredientSoft,
        Ingredient.BloodBean => SfxCue.BloodBeanPour,
        _ => SfxCue.None,
    };
}
