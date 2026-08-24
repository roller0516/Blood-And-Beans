using UnityEngine;

/// Long cook time (doc 5.1) — desserts also take more trips to load.
public class Oven : Station
{
    void Reset() => cookSeconds = 9f;   // ponytail: placeholder, doc 14장 has no timings

    protected override bool AcceptsIngredient(Ingredient ingredient, int currentCount) =>
        currentCount == 0
            ? ingredient == Ingredient.BreadBase
            : ingredient == Ingredient.Chocolate || ingredient == Ingredient.Almond ||
              ingredient == Ingredient.Cream || ingredient == Ingredient.Berry;
}
