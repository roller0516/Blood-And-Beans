using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;

/// `TextKey`의 상수가 전부 `Localization` 표에 있는지 본다. 키만 더하고 엑셀을 안 고쳤거나
/// 엑셀에서 키를 지웠을 때, 화면에 `[Hud.Day]` 같은 글자가 뜨기 전에 여기서 잡는다.
public class TextKeyTests
{
    static IEnumerable<string> AllKeys()
    {
        foreach (var group in typeof(TextKey).GetNestedTypes(BindingFlags.Public))
            foreach (var field in group.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
                if (field.IsLiteral && field.FieldType == typeof(string))
                    yield return (string)field.GetRawConstantValue();
    }

    [Test]
    public void 모든_키가_표에_있다()
    {
        var missing = new List<string>();
        foreach (var key in AllKeys())
            if (L10n.Get(key) == $"[{key}]") missing.Add(key);

        Assert.IsEmpty(missing, "Localization.xlsx에 없는 키: " + string.Join(", ", missing));
    }

    /// `DisplayNames`는 열거자 이름으로 키를 만든다. 열거자를 늘리고 시트에 행을 안 더하면 화면에
    /// `[Menu.NewCake]`가 뜨므로, 정의된 열거자 전부를 한 번씩 읽어 본다.
    [Test]
    public void 모든_열거자_이름이_표에_있다()
    {
        var missing = new List<string>();
        void Check(string text)
        {
            if (text.StartsWith("[") && text.EndsWith("]")) missing.Add(text);
        }

        foreach (MenuId v in Enum.GetValues(typeof(MenuId))) Check(DisplayNames.Of(v));
        foreach (Ingredient v in Enum.GetValues(typeof(Ingredient))) Check(DisplayNames.Of(v));
        foreach (Race v in Enum.GetValues(typeof(Race))) Check(DisplayNames.Of(v));
        foreach (CharacterId v in Enum.GetValues(typeof(CharacterId))) Check(DisplayNames.Of(v));
        foreach (Gem v in Enum.GetValues(typeof(Gem)))
        {
            Check(DisplayNames.Of(v));
            Check(DisplayNames.EffectOf(v));
        }
        foreach (DaySkill v in Enum.GetValues(typeof(DaySkill)))
        {
            Check(DisplayNames.Of(v));
            if (v != DaySkill.None) Check(DisplayNames.EffectOf(v));
        }

        // 밤 액티브는 캐릭터가 쓰는 것만 문구가 있다 (옛 캐릭터의 스킬은 시트에 없다).
        Check(DisplayNames.Of(NightSkill.None));
        foreach (var c in CharacterCatalog.All)
        {
            Check(DisplayNames.Of(c.Night));
            Check(DisplayNames.EffectOf(c.Night));
        }

        Assert.IsEmpty(missing, "Localization.xlsx에 없는 행: " + string.Join(", ", missing));
    }

    [Test]
    public void 키가_한_개_이상_있다()
    {
        Assert.Greater(new List<string>(AllKeys()).Count, 0);
    }
}
