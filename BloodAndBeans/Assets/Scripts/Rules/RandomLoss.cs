using System;
using System.Collections.Generic;

public static class RandomLoss
{
    public static List<Ingredient> TakeHalf(List<Ingredient> items, Random rng)
    {
        var lost = new List<Ingredient>();
        if (items == null || rng == null) return lost;

        var count = (items.Count + 1) / 2;
        for (var i = 0; i < count; i++)
        {
            var index = rng.Next(items.Count);
            lost.Add(items[index]);
            items.RemoveAt(index);
        }
        return lost;
    }
}
