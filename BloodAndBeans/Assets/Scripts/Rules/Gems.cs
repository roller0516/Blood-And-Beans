/// 보석 6종 (기획서 8.2). 순서는 `Gems.Items`와 같다.
public enum Gem { Ember, Scales, Wind, Foam, Engraving, Tea }

/// 보석의 효과·기간·드롭 확률 (기획서 8장, 6.5.2).
public static class Gems
{
    // 기획서 8.1: 6종 모두 3턴(낮 3회).
    public static int Turns => Balance.Current.GemTurns;

    // 기획서 8.2 수치. 여섯 배수는 `BalanceData.GemScale` 한 줄에 `Gem` 순서로 들어 있다.
    public static float CookTimeScale => ScaleOf(Gem.Ember);        // 불씨 — 조리 시간 -30%
    public static float PerfectWidthScale => ScaleOf(Gem.Scales);   // 저울 — Perfect 폭 +40%
    public static float MoveSpeedScale => ScaleOf(Gem.Wind);        // 바람 — 이동 속도 +12%
    public static float WashTimeScale => ScaleOf(Gem.Foam);         // 거품 — 세척 시간 -35%
    public static float CooldownScale => ScaleOf(Gem.Engraving);    // 각인 — 낮·밤 액티브 쿨타임 -25%
    public static float PatienceScale => ScaleOf(Gem.Tea);          // 찻잎 — 손님 인내심 +20%

    public static float ScaleOf(Gem gem) => Balance.Current.GemScale[(int)gem];

    public static readonly Gem[] All = { Gem.Ember, Gem.Scales, Gem.Wind, Gem.Foam, Gem.Engraving, Gem.Tea };

    static Ingredient[] Items => Balance.Current.GemItems;

    public static Ingredient ItemOf(Gem gem) => Items[(int)gem];
    public static string NameOf(Gem gem) => Balance.Current.GemNames[(int)gem];
    public static string EffectOf(Gem gem) => Balance.Current.GemEffects[(int)gem];

    public static bool TryOf(Ingredient item, out Gem gem)
    {
        var i = System.Array.IndexOf(Items, item);
        gem = i < 0 ? default : (Gem)i;
        return i >= 0;
    }

    public static bool IsGem(Ingredient item) => System.Array.IndexOf(Items, item) >= 0;

    /// `roll`은 [0,1) 난수. 3등급은 확정, 2등급은 일차 확률, 1등급은 없다 (기획서 6.5.2).
    public static bool DropsGem(int tier, int day, double roll) =>
        tier >= 3 || tier == 2 && roll < Balance.ByDay(Balance.Current.Tier2GemChance, day);

    /// 블러드 빈은 3등급에만, 한 상자에 최대 1개다 (기획서 6.5.2).
    public static bool DropsBloodBean(int tier, int day, double roll) =>
        tier >= 3 && roll < Balance.ByDay(Balance.Current.Tier3BloodBeanChance, day);
}

/// 한 팀의 보석 기간. 같은 보석은 효과를 겹치지 않고 턴만 3턴으로 갱신한다 (기획서 8.1).
public sealed class TeamGems
{
    readonly int[] lastDay = new int[Gems.All.Length];

    /// `day`는 획득한 밤의 일차이고 같은 번호의 낮부터 센다. 이미 켜져 있었으면 true(갱신).
    public bool Apply(Gem gem, int day)
    {
        if (day < 1) return false;
        var refreshed = Remaining(gem, day) > 0;
        lastDay[(int)gem] = day + Gems.Turns - 1;
        return refreshed;
    }

    public int Remaining(Gem gem, int day) =>
        day < 1 ? 0 : System.Math.Max(0, lastDay[(int)gem] - day + 1);
}
