using UnityEngine;

/// 제작 시간이 길다 (기획서 5.1). 디저트는 재료를 넣는 횟수도 더 많다.
public class Oven : Station
{
    void Reset() => cookSeconds = 9f;   // ponytail: 임시값. 기획서 14장에 시간 표가 없다

    protected override bool AcceptsIngredient(Ingredient ingredient, int currentCount) =>
        currentCount == 0
            ? ingredient == Ingredient.BreadBase
            : ingredient == Ingredient.Chocolate || ingredient == Ingredient.Almond ||
              ingredient == Ingredient.Cream || ingredient == Ingredient.Berry;
}
