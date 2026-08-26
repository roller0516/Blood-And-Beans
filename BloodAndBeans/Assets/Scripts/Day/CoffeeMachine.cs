using UnityEngine;

/// 제작 시간이 짧다 (기획서 5.1). 그 외에는 기반 클래스와 다르지 않다.
public class CoffeeMachine : Station
{
    void Reset() => cookSeconds = 4f;   // ponytail: 임시값. 기획서 14장에 시간 표가 없다

    protected override bool AcceptsIngredient(Ingredient ingredient, int currentCount) =>
        currentCount == 0
            ? ingredient == Ingredient.Bean || ingredient == Ingredient.BloodBean
            : ingredient == Ingredient.Milk || ingredient == Ingredient.Cream ||
              ingredient == Ingredient.Chocolate || ingredient == Ingredient.Ice;
}
