/// 캐릭터 4종의 외형 ID (기획서 9.1.1). 모델·초상 표의 키다.
///
/// 0~4는 옛 5종(도깨비·박쥐·연금술사·하운드·미믹)이 쓰던 값이다. 표시 애셋에 직렬화된 옛 값이
/// 새 캐릭터로 조용히 읽히지 않게 재사용하지 않는다.
public enum CharacterId
{
    Vampire = 5,
    Zombie = 6,
    Werewolf = 7,
    Witch = 8,
}

/// 밤 액티브 스킬 (기획서 9.2). 쿨타임이 길고 캐릭터당 하나다.
public enum NightSkill
{
    /// 아직 정해지지 않은 칸. 기획서 9.2의 후보가 낮 패시브 수보다 적다.
    None,

    /// 도깨비불 — 가짜 아이템 박스를 설치한다 (쿨 18초).
    WillOWisp,

    /// 메아리 — 주변 넓은 범위의 안개를 즉시 걷어낸다 (쿨 18초).
    Echo,

    /// 감별 — 박스의 가려진 슬롯이 즉시 공개된다 (쿨 16초).
    Appraise,

    /// 추적 — 주변의 숨겨진 가방을 찾아낸다 (쿨 14초).
    Track,

    /// 환각 — 가짜로 숨겨진 가방을 심는다 (쿨 20초).
    Illusion,

    /// 박쥐 떼 — 맞은 적의 시야가 코앞으로 좁아진다 (뱀파이어).
    BatSwarm,

    /// 팔 던지기 — 맞은 적이 움직이지 못한다 (좀비).
    ArmThrow,

    /// 미끄러운 물약 — 웅덩이에 들어온 적이 넘어진다 (마녀).
    SlipperyPotion,
}

/// 밤 액티브의 쿨타임 (기획서 9.2 표).
public static class NightSkills
{
    /// 표의 순서는 `NightSkill` 열거자와 같다 (`BalanceData.NightSkillCooldown`).
    public static float CooldownOf(NightSkill s)
    {
        var table = Balance.Current.NightSkillCooldown;
        var i = (int)s;
        return i >= 0 && i < table.Length ? table[i] : 0f;
    }

    public static bool Exists(NightSkill s) => s != NightSkill.None;

    /// 밤 액티브의 판정 반경과 지속 (기획서 9.2). 표는 `BalanceData`에 있고 엑셀에서 온다
    /// (`CommonDataTable`의 skills 구역).
    ///
    /// **`[SerializeField]`로 들지 않는다.** 기획이 정하는 게임 수치라 프리팹에 박으면
    /// 표와 프리팹 둘이 되고, 표를 고쳐도 게임은 프리팹 값으로 돈다 — 실제로 그랬다.
    public static float EchoRadius => Balance.Current.EchoRadius;
    public static float AppraiseRadius => Balance.Current.AppraiseRadius;
    public static float TrackRadius => Balance.Current.TrackRadius;
    public static float TrackRevealSeconds => Balance.Current.TrackRevealSeconds;

    public static float BatSwarmRange => Balance.Current.BatSwarmRange;
    public static float BatSwarmSeconds => Balance.Current.BatSwarmSeconds;
    public static float BatSwarmVision => Balance.Current.BatSwarmVision;
    public static float ArmThrowRange => Balance.Current.ArmThrowRange;
    public static float ArmThrowSeconds => Balance.Current.ArmThrowSeconds;
    public static float PotionRange => Balance.Current.PotionRange;
    public static float PotionRadius => Balance.Current.PotionRadius;
    public static float PotionSeconds => Balance.Current.PotionSeconds;
    public static float PotionFallSeconds => Balance.Current.PotionFallSeconds;
    public static float PotionImmuneSeconds => Balance.Current.PotionImmuneSeconds;
}

/// 낮 액티브 (기획서 9.1.2). 낮에만 쓰고 캐릭터당 하나다.
public enum DaySkill
{
    None,

    /// 불붙이기 — 점유 중인 설비의 남은 조리 시간을 줄이고, 놓은 뒤 설비가 달아오른다.
    Ignite,

    /// 활공 — 짧은 시간 이동속도가 크게 오른다.
    Glide,

    /// 정제 — 다음 한 잔의 완성 게이지를 Perfect로 확정한다.
    Refine,

    /// 지름길 — 짧은 시간 다른 캐릭터를 통과한다.
    Shortcut,

    /// 삼키기 — 들고 있는 더러운 식기의 세척을 70% 진행시킨다.
    Swallow,

    /// 관 떨어뜨리기 — 지정한 자리에 관이 떨어져 벽이 된다 (뱀파이어).
    CoffinDrop,

    /// 물기 — 문 적이 좀비걸음이 되고, 부딪힌 사람에게 옮는다 (좀비).
    Bite,

    /// 하울링 — 다른 팀은 도망치고 같은 팀은 빨라진다 (늑대인간).
    Howl,

    /// 개구리 저주 — 맞은 적이 아무것도 집거나 쓸 수 없다 (마녀).
    FrogCurse,
}

/// 낮 액티브의 짝과 수치 (기획서 9.1.1~9.1.3).
public static class DaySkills
{
    /// 9.1.1 표: 컨셉은 밤 액티브가 정하고 낮 액티브는 그 컨셉을 따라온다.
    /// 짝은 `BalanceData.DaySkillOfNight`에 `NightSkill` 순서로 들어 있다.
    public static DaySkill Of(NightSkill night) =>
        Pick(Balance.Current.DaySkillOfNight, (int)night, DaySkill.None);

    /// 9.1.3 표. 순서는 `DaySkill` 열거자와 같다.
    public static float CooldownOf(DaySkill s) =>
        Pick(Balance.Current.DaySkillCooldown, (int)s, 0f);

    /// 9.1.3 표의 지속.
    public static float GlideSeconds => Balance.Current.GlideSeconds;
    public static float ShortcutSeconds => Balance.Current.ShortcutSeconds;

    /// 9.1.2 삼키기: 세척 70% 진행 → 개수대 점유 3초가 0.9초.
    public static float SwallowProgress => Balance.Current.SwallowProgress;

    public static float GlideSpeed => Balance.Current.GlideSpeed;
    public static float IgniteCut => Balance.Current.IgniteCut;
    public static float OverheatSeconds => Balance.Current.OverheatSeconds;

    public static float CoffinRange => Balance.Current.CoffinRange;
    public static float CoffinWidth => Balance.Current.CoffinWidth;
    public static float CoffinDepth => Balance.Current.CoffinDepth;
    public static float CoffinSeconds => Balance.Current.CoffinSeconds;
    public static float BiteRange => Balance.Current.BiteRange;
    public static float BiteSeconds => Balance.Current.BiteSeconds;
    public static float BiteSpeed => Balance.Current.BiteSpeed;
    public static float BiteImmuneSeconds => Balance.Current.BiteImmuneSeconds;
    public static float HowlRadius => Balance.Current.HowlRadius;
    public static float HowlFleeSeconds => Balance.Current.HowlFleeSeconds;
    public static float HowlAllySpeed => Balance.Current.HowlAllySpeed;
    public static float HowlAllySeconds => Balance.Current.HowlAllySeconds;
    public static float FrogRange => Balance.Current.FrogRange;
    public static float FrogSeconds => Balance.Current.FrogSeconds;
    public static float FrogSpeed => Balance.Current.FrogSpeed;

    /// 표 밖의 값은 기본값으로 떨어뜨린다. 데이터가 짧아도 예외를 던지지 않는다.
    static T Pick<T>(T[] table, int index, T fallback) =>
        index >= 0 && index < table.Length ? table[index] : fallback;
}

/// 캐릭터 한 종의 정의 (기획서 9장).
///
/// 캐릭터 하나 = 몬스터 컨셉 + 낮 액티브 1 + 밤 액티브 1 (9.1). 이름과 한 줄 설명은 여기 없다 —
/// `DisplayNames`가 `Id`·`Day`·`Night`로 Localization 표에서 꺼낸다.
public readonly struct CharacterDef
{
    /// 외형 키. 모델·초상 표가 읽는다.
    public readonly CharacterId Id;
    public readonly DaySkill Day;
    public readonly NightSkill Night;

    public CharacterDef(CharacterId id, NightSkill night)
    {
        Id = id;
        Night = night;
        Day = DaySkills.Of(night);   // 9.1.1: 낮 액티브는 밤 액티브의 짝으로 정해진다
    }
}

/// 캐릭터 4종 (기획서 9.1.1). 배열 인덱스가 곧 픽 번호다.
public static class CharacterCatalog
{
    /// 표는 `BalanceData.Characters`에 있다. 낮 액티브는 밤 액티브에서 유도하므로
    /// (`CharacterDef` 생성자) 표가 바뀔 때만 다시 만든다.
    public static CharacterDef[] All => all.Value;

    static readonly Derived<CharacterDef[]> all = new(Build);

    static CharacterDef[] Build(BalanceData data)
    {
        var rows = data.Characters;
        var outp = new CharacterDef[rows.Length];
        for (var i = 0; i < rows.Length; i++)
            outp[i] = new CharacterDef(rows[i].Id, rows[i].Night);
        return outp;
    }

    public static bool IsValid(int index) => index >= 0 && index < All.Length;

    /// 고르지 않은 상태. 팀 내 중복 픽 판정에서 "아무도 안 골랐다"와 구별해야 한다.
    public const int NoPick = -1;
}
