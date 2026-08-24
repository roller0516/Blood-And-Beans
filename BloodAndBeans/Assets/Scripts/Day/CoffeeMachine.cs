using UnityEngine;

/// Short cook time (doc 5.1). Nothing else differs from the base.
public class CoffeeMachine : Station
{
    void Reset() => cookSeconds = 4f;   // ponytail: placeholder, doc 14장 has no timings

    protected override bool AcceptsIngredient(Ingredient ingredient, int currentCount) =>
        currentCount == 0
            ? ingredient == Ingredient.Bean || ingredient == Ingredient.BloodBean
            : ingredient == Ingredient.Milk || ingredient == Ingredient.Cream ||
              ingredient == Ingredient.Chocolate || ingredient == Ingredient.Ice;
}
