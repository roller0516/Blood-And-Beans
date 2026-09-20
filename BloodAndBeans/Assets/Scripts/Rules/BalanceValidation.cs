using System;
using System.Collections.Generic;

/// `BalanceData`의 표들이 열거자·서로와 정렬돼 있는지 검사한다.
///
/// **왜 필요한가.** 표 대부분이 열거자 순서에 정렬돼 있는데 그 계약이 주석에만 있다.
/// 열거자 중간에 값을 하나 끼워 넣으면 표가 통째로 밀리는데 컴파일 오류가 나지 않고,
/// `CharacterCatalog.Pick`이 범위 밖에서 조용히 폴백하므로 예외도 없다 — 쿨타임이 0이 되어
/// 스킬이 무한 연타가 되는 식으로만 드러난다. 값이 엑셀 → `DataTableAsset` → `Rebuild` →
/// 배열 네 단계를 거치므로 어긋날 경로도 넓다.
///
/// `BB.Rules`라 `UnityEngine`을 쓰지 않는다. **로그는 부르는 쪽이 찍는다** — 부팅 때는
/// `DataManager`가, 빌드 전에는 EditMode 테스트가 같은 함수를 부른다.
public static class BalanceValidation
{
    /// 어긋난 항목을 모두 모아 돌려준다. 빈 목록이면 정상이다.
    /// 첫 항목에서 멈추지 않는 이유는 엑셀 한 줄이 여러 표를 동시에 밀기 때문이다.
    public static List<string> Problems(BalanceData d)
    {
        var problems = new List<string>();
        if (d == null)
        {
            problems.Add("BalanceData가 null이다.");
            return problems;
        }

        // 열거자 순서에 정렬된 표. 기대 길이를 열거자에서 뽑으므로 열거자가 늘면 함께 따라온다.
        Expect(problems, nameof(d.GemScale), d.GemScale.Length, Count<Gem>(), "Gem 순서");
        Expect(problems, nameof(d.GemNames), d.GemNames.Length, Count<Gem>(), "Gem 순서");
        Expect(problems, nameof(d.GemEffects), d.GemEffects.Length, Count<Gem>(), "Gem 순서");
        Expect(problems, nameof(d.GemItems), d.GemItems.Length, Count<Gem>(), "Gem 순서");
        Expect(problems, nameof(d.RaceNames), d.RaceNames.Length, Count<Race>(), "Race 순서");
        Expect(problems, nameof(d.GaugeMultiplier), d.GaugeMultiplier.Length, Count<Gauge>(), "Gauge 순서");

        // MenuId와 Ingredient는 None = -1을 갖는다. 표는 0부터라 그만큼 짧다.
        Expect(problems, nameof(d.MenuNames), d.MenuNames.Length, Count<MenuId>() - 1, "MenuId 순서, None 제외");

        // 재료 표는 숲·상비 재료까지만이다. 보석(10~15)은 UpgradePart 값을 빌려 쓴다.
        var forestItems = (int)Ingredient.BreadBase + 1;
        Expect(problems, nameof(d.IngredientNames), d.IngredientNames.Length, forestItems, "Ingredient 0~9");
        Expect(problems, nameof(d.IngredientWeight), d.IngredientWeight.Length, forestItems, "Ingredient 0~9");

        // 스킬 표는 [0]이 None이라 열거자 길이와 같다.
        Expect(problems, nameof(d.DaySkillCooldown), d.DaySkillCooldown.Length, Count<DaySkill>(), "DaySkill 순서");
        Expect(problems, nameof(d.DaySkillNames), d.DaySkillNames.Length, Count<DaySkill>(), "DaySkill 순서");
        Expect(problems, nameof(d.DaySkillEffects), d.DaySkillEffects.Length, Count<DaySkill>(), "DaySkill 순서");
        Expect(problems, nameof(d.NightSkillCooldown), d.NightSkillCooldown.Length, Count<NightSkill>(), "NightSkill 순서");
        Expect(problems, nameof(d.DaySkillOfNight), d.DaySkillOfNight.Length, Count<NightSkill>(), "NightSkill 순서");

        Expect(problems, nameof(d.ZoneShares), d.ZoneShares.Length, Count<ForestRings.Zone>(), "Zone 순서");

        // 캐릭터는 배열 인덱스가 곧 픽 번호다 (기획서 9.1.1).
        Expect(problems, nameof(d.Characters), d.Characters.Length, Count<CharacterId>(), "CharacterId 종류 수");
        DistinctNightSkills(problems, d);

        // 일차로 인덱스하는 표. TotalDays가 바뀌면 전부 따라와야 한다 (기획서 4장).
        Expect(problems, nameof(d.RentByDay), d.RentByDay.Length, d.TotalDays, "일차 수");
        Expect(problems, nameof(d.Tier2GemChance), d.Tier2GemChance.Length, d.TotalDays, "일차 수");
        Expect(problems, nameof(d.Tier3BloodBeanChance), d.Tier3BloodBeanChance.Length, d.TotalDays, "일차 수");
        for (var i = 0; i < d.RegenWeights.Length; i++)
            Expect(problems, $"{nameof(d.RegenWeights)}[{d.RegenWeights[i].Item}].ByDay",
                   d.RegenWeights[i].ByDay.Length, d.TotalDays, "일차 수");

        Expect(problems, nameof(d.TierTable), d.TierTable.Length, Count<ForestRings.Zone>(), "Zone 순서");
        for (var i = 0; i < d.TierTable.Length; i++)
            Expect(problems, $"{nameof(d.TierTable)}[{i}]", d.TierTable[i].Length, d.TotalDays, "일차 수");

        // 서로 길이가 묶인 표.
        Expect(problems, nameof(d.PenaltyMoveLoss), d.PenaltyMoveLoss.Length, d.PenaltyCraftLoss.Length, "페널티 단계 수");
        Expect(problems, nameof(d.PenaltyVisionLoss), d.PenaltyVisionLoss.Length, d.PenaltyCraftLoss.Length, "페널티 단계 수");
        Expect(problems, nameof(d.PenaltyOpenLoss), d.PenaltyOpenLoss.Length, d.PenaltyCraftLoss.Length, "페널티 단계 수");
        Expect(problems, nameof(d.LootSlotMax), d.LootSlotMax.Length, d.LootSlotMin.Length, "상자 등급 수");

        // 밴드는 상한보다 하나 많다 — 전부 넘으면 마지막 밴드다 (기획서 6.7).
        Expect(problems, nameof(d.LoadBandSpeed), d.LoadBandSpeed.Length, d.LoadBandMax.Length + 1, "LoadBandMax + 1");

        ZoneSharesSum(problems, d);

        // 엑셀에서 **중간** 행을 지우면 길이는 그대로고 그 칸만 기본값으로 남는다
        // (임포터가 키로 찍어 넣으므로). 길이 검사로는 안 잡히는 쪽이라 따로 본다.
        NoNulls(problems, nameof(d.GemNames), d.GemNames);
        NoNulls(problems, nameof(d.GemEffects), d.GemEffects);
        NoNulls(problems, nameof(d.RaceNames), d.RaceNames);
        NoNulls(problems, nameof(d.MenuNames), d.MenuNames);
        NoNulls(problems, nameof(d.IngredientNames), d.IngredientNames);
        NoNulls(problems, nameof(d.DaySkillNames), d.DaySkillNames);
        NoNulls(problems, nameof(d.DaySkillEffects), d.DaySkillEffects);
        NoCharacterHoles(problems, d);

        // [0]은 None이라 0이 맞고, 나머지가 0이면 그 스킬이 무한 연타가 된다.
        NonZeroAfterNone(problems, nameof(d.DaySkillCooldown), d.DaySkillCooldown);
        NonZeroAfterNone(problems, nameof(d.NightSkillCooldown), d.NightSkillCooldown);

        return problems;
    }

    /// `null`은 임포터가 그 칸을 채우지 않았다는 뜻이다. 빈 문자열은 의도된 값일 수 있어
    /// (`DaySkillEffects[0]`이 None이다) 문제로 세지 않는다.
    static void NoNulls(List<string> problems, string name, string[] table)
    {
        for (var i = 0; i < table.Length; i++)
            if (table[i] == null)
                problems.Add($"{name}[{i}]: 비어 있다. 엑셀에 그 행이 없다.");
    }

    static void NoCharacterHoles(List<string> problems, BalanceData d)
    {
        for (var i = 0; i < d.Characters.Length; i++)
            if (d.Characters[i].Name == null)
                problems.Add($"{nameof(d.Characters)}[{i}]: 비어 있다. 엑셀에 픽 번호 {i} 행이 없다.");
    }

    static void NonZeroAfterNone(List<string> problems, string name, float[] table)
    {
        for (var i = 1; i < table.Length; i++)
            if (table[i] <= 0f)
                problems.Add($"{name}[{i}]: 쿨타임이 {table[i]}다. 0이면 무한 연타가 된다.");
    }

    /// 캐릭터마다 밤 액티브가 하나씩이다 (기획서 9.1.1). 겹치면 한 스킬이 화면에서 사라진다.
    static void DistinctNightSkills(List<string> problems, BalanceData d)
    {
        var seen = new HashSet<NightSkill>();
        foreach (var c in d.Characters)
            if (!seen.Add(c.Night))
                problems.Add($"{nameof(d.Characters)}: 밤 액티브 {c.Night}가 둘 이상에 배정됐다 ({c.Name}).");
    }

    /// 구역 배분은 퍼센트라 100이어야 한다 (기획서 6.3.1).
    static void ZoneSharesSum(List<string> problems, BalanceData d)
    {
        var sum = 0;
        foreach (var share in d.ZoneShares) sum += share;
        if (sum != 100) problems.Add($"{nameof(d.ZoneShares)}: 합이 {sum}이다. 100이어야 한다.");
    }

    static void Expect(List<string> problems, string name, int actual, int expected, string reason)
    {
        if (actual != expected)
            problems.Add($"{name}: 길이 {actual}, 기대 {expected} ({reason}).");
    }

    static int Count<T>() where T : struct, Enum => Enum.GetValues(typeof(T)).Length;
}
