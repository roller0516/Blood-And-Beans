using System.Collections.Generic;

/// 사용자에게 보이는 이름과 설명. 재료·메뉴·종족·보석·스킬·캐릭터의 한글 표기를 한 곳에 모은다.
///
/// 화면마다 문자열을 박아 두면 같은 재료가 화면에 따라 다른 이름으로 나온다. 문구는
/// `Localization` 표에 있고, 키는 열거자 이름에서 만든다 (`Ingredient.Milk`,
/// `DaySkill.Glide.Effect`). 열거자를 늘리면 시트에 같은 이름의 행을 더한다 —
/// `TextKeyTests`가 빠진 행을 잡는다.
///
/// `BB.Rules`가 아니라 `BB.Game`에 두는 이유는 `BB.Rules`가 UnityEngine을 못 써서
/// Localization을 부를 수 없기 때문이다. 규칙은 열거자만 알고, 이름은 여기서 푼다.
public static class DisplayNames
{
    /// 표에 없는 열거자(`None`, 범위 밖)를 그릴 때. 글자가 아니라 기호라 번역하지 않는다.
    const string Missing = "—";

    public static string Of(MenuId menu) => NameOf("Menu", menu);

    public static string Of(Ingredient item)
    {
        if (Gems.TryOf(item, out var gem)) return L10n.Format(TextKey.Names.GemItem, Of(gem));
        return NameOf("Ingredient", item);
    }

    public static string Of(Race race) => NameOf("Race", race);

    public static string Of(Gem gem) => NameOf("Gem", gem);
    public static string EffectOf(Gem gem) => EffectOf("Gem", gem);

    public static string Of(DaySkill skill) => NameOf("DaySkill", skill);
    public static string EffectOf(DaySkill skill) => EffectOf("DaySkill", skill);

    public static string Of(NightSkill skill) => NameOf("NightSkill", skill);
    public static string EffectOf(NightSkill skill) => EffectOf("NightSkill", skill);

    public static string Of(CharacterId id) => NameOf("Character", id);

    /// 팀 표기. 내부 인덱스는 0부터지만 화면에는 1부터 센 번호를 보인다.
    /// 배정 전(음수)에 번호를 찍으면 「0팀」이 나오므로 대기 문구를 따로 준다.
    public static string Team(int team) => team < 0
        ? L10n.Get(TextKey.Names.TeamPending)
        : L10n.Format(TextKey.Names.Team, team + 1);

    static string NameOf<T>(string group, T value) where T : struct, System.Enum
    {
        var key = EnumKey<T>.Name(group, value);
        return key == null ? Missing : L10n.Get(key);
    }

    /// 효과가 없는 항목(`DaySkill.None`)은 빈 문자열이다 — 시트에 빈 칸을 두면 누락과 구별이 안 된다.
    static string EffectOf<T>(string group, T value) where T : struct, System.Enum
    {
        var key = EnumKey<T>.Effect(group, value);
        return key == null ? string.Empty : L10n.Get(key);
    }

    /// 열거자 값 → 키. 문자열 결합을 값마다 한 번만 하려고 캐시한다. 정의되지 않은 값은 `null`이다.
    static class EnumKey<T> where T : struct, System.Enum
    {
        static readonly Dictionary<T, string> names = new();
        static readonly Dictionary<T, string> effects = new();

        public static string Name(string group, T value)
        {
            if (names.TryGetValue(value, out var key)) return key;
            key = System.Enum.IsDefined(typeof(T), value) ? $"{group}.{value}" : null;
            names[value] = key;
            return key;
        }

        public static string Effect(string group, T value)
        {
            if (effects.TryGetValue(value, out var key)) return key;
            var name = Name(group, value);
            key = name != null && !IsNone(value) ? name + ".Effect" : null;
            effects[value] = key;
            return key;
        }

        /// 열거자의 `None`은 이름만 있고 효과 문구가 없다.
        static bool IsNone(T value) => value.ToString() == "None";
    }
}
