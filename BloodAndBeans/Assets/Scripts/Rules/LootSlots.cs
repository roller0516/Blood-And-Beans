using System.Collections.Generic;

/// 상자 한 칸. 같은 종류는 한 칸에 쌓인다 — 칸 제한은 개수가 아니라 *종류* 기준이다.
public struct LootStack
{
    public Ingredient Item;
    public int Count;

    public LootStack(Ingredient item, int count)
    {
        Item = item;
        Count = count;
    }
}

/// 상자 칸 규칙. 씬도 네트워크도 없이 기획과 대조할 수 있게 BB.Rules에 둔다.
public static class LootSlots
{
    /// 상자 하나가 담는 최대 *종류* 수. 개수 상한이 아니다.
    public const int MaxTypes = 5;

    /// 아이템 목록을 종류별로 접고, 종류가 `MaxTypes`를 넘으면 상자를 쪼갠다.
    /// 12종류를 버리면 5/5/2로 임시 상자 세 개가 된다.
    ///
    /// 처음 나온 순서를 유지한다. 딕셔너리 순회 순서에 맡기면 같은 가방을 두 번 버려도
    /// 칸 배치가 달라져서 무엇이 어느 상자에 들어갔는지 재현할 수 없다.
    public static List<List<LootStack>> Pack(IEnumerable<Ingredient> items)
    {
        var order = new List<Ingredient>();
        var counts = new Dictionary<Ingredient, int>();

        if (items != null)
            foreach (var item in items)
            {
                if (item == Ingredient.None) continue;
                if (!counts.ContainsKey(item))
                {
                    counts[item] = 0;
                    order.Add(item);
                }
                counts[item]++;
            }

        var boxes = new List<List<LootStack>>();
        for (var at = 0; at < order.Count; at += MaxTypes)
        {
            var box = new List<LootStack>();
            for (var i = at; i < order.Count && i < at + MaxTypes; i++)
                box.Add(new LootStack(order[i], counts[order[i]]));
            boxes.Add(box);
        }
        return boxes;
    }

    /// 공개는 개봉 시각부터 `interval`마다 한 칸씩, 건너뛰기 없이 진행된다.
    /// 서버와 클라이언트가 같은 식을 써야 화면과 실제 담기 가능 여부가 어긋나지 않는다.
    public static int RevealedCount(double now, double openedAt, float interval, int slotCount)
    {
        if (slotCount <= 0) return 0;
        if (interval <= 0f) return slotCount;

        var steps = (int)((now - openedAt) / interval);
        return steps < 0 ? 0 : steps > slotCount ? slotCount : steps;
    }
}
