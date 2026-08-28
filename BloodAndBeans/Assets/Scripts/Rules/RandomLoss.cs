using System;
using System.Collections.Generic;

public static class RandomLoss
{
    public static List<Ingredient> TakeHalf(List<Ingredient> items, Random rng) =>
        TakeShare(items, 0.5f, rng);

    /// 목록에서 `share` 비율만큼 무작위로 덜어내고 덜어낸 것을 돌려준다.
    /// 개수는 올림이다 — 한 개를 들고 귀환에 실패했는데 아무것도 잃지 않으면 안 된다.
    ///
    /// 귀환 규칙이 절반 고정에서 "소환 위치 밖이면 일부(n%)"로 바뀌면서 비율을 인자로 뺐다.
    public static List<Ingredient> TakeShare(List<Ingredient> items, float share, Random rng)
    {
        var lost = new List<Ingredient>();
        if (items == null || rng == null || share <= 0f) return lost;

        var count = share >= 1f
            ? items.Count
            : (int)Math.Ceiling(items.Count * (double)share);

        for (var i = 0; i < count && items.Count > 0; i++)
        {
            var index = rng.Next(items.Count);
            lost.Add(items[index]);
            items.RemoveAt(index);
        }
        return lost;
    }
}
