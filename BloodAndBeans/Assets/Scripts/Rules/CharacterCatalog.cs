/// 캐릭터 5종의 외형 ID (기획서 9.1.1). 모델·초상 표의 키다.
///
/// 정수값은 옛 낮 패시브 enum과 같다. 표시 애셋이 이 값을 직렬화해 두었으므로 바꾸지 않는다.
public enum CharacterId
{
    Dokkaebi = 0,
    Bat = 1,
    Alchemist = 2,
    Hound = 3,
    Mimic = 4,
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
}

/// 밤 액티브의 쿨타임 (기획서 9.2 표).
public static class NightSkills
{
    public static float CooldownOf(NightSkill s) => s switch
    {
        NightSkill.WillOWisp => 18f,
        NightSkill.Echo => 18f,
        NightSkill.Appraise => 16f,
        NightSkill.Track => 14f,
        NightSkill.Illusion => 20f,
        _ => 0f,
    };

    public static bool Exists(NightSkill s) => s != NightSkill.None;
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
}

/// 낮 액티브의 짝과 수치 (기획서 9.1.1~9.1.3).
public static class DaySkills
{
    /// 9.1.1 표: 컨셉은 밤 액티브가 정하고 낮 액티브는 그 컨셉을 따라온다.
    public static DaySkill Of(NightSkill night) => night switch
    {
        NightSkill.WillOWisp => DaySkill.Ignite,
        NightSkill.Echo => DaySkill.Glide,
        NightSkill.Appraise => DaySkill.Refine,
        NightSkill.Track => DaySkill.Shortcut,
        NightSkill.Illusion => DaySkill.Swallow,
        _ => DaySkill.None,
    };

    /// 9.1.3 표.
    public static float CooldownOf(DaySkill s) => s switch
    {
        DaySkill.Shortcut => 14f,
        DaySkill.Ignite => 15f,
        DaySkill.Refine => 18f,
        DaySkill.Glide => 22f,
        DaySkill.Swallow => 22f,
        _ => 0f,
    };

    /// 9.1.3 표의 지속.
    public static readonly float GlideSeconds = 3f;
    public static readonly float ShortcutSeconds = 2.5f;

    /// 9.1.2 삼키기: 세척 70% 진행 → 개수대 점유 3초가 0.9초.
    public static readonly float SwallowProgress = 0.7f;

    // ponytail: 9.1.2는 "크게 오른다"뿐이고 폭은 9.1의 +25~40%다. 상한으로 박고 수치가 정해지면 교체한다.
    public static readonly float GlideSpeed = 1.4f;

    // ponytail: 9.1.2 불붙이기에 수치가 없다. 9.1 폭 상한 40% · 과열 3초로 박고 수치가 정해지면 교체한다.
    public static readonly float IgniteCut = 0.4f;
    public static readonly float OverheatSeconds = 3f;

    public static string NameOf(DaySkill s) => s switch
    {
        DaySkill.Ignite => "불붙이기",
        DaySkill.Glide => "활공",
        DaySkill.Refine => "정제",
        DaySkill.Shortcut => "지름길",
        DaySkill.Swallow => "삼키기",
        _ => "없음",
    };

    public static string EffectOf(DaySkill s) => s switch
    {
        DaySkill.Ignite => "쓰고 있는 설비의 남은 조리 시간을 줄인다. 놓은 뒤 설비가 잠깐 달아오른다 (쿨 15초)",
        DaySkill.Glide => "3초 동안 이동속도가 크게 오른다 (쿨 22초)",
        DaySkill.Refine => "다음 한 잔의 완성 게이지가 Perfect로 확정된다 (쿨 18초)",
        DaySkill.Shortcut => "2.5초 동안 다른 캐릭터를 통과한다 (쿨 14초)",
        DaySkill.Swallow => "들고 있는 더러운 식기의 세척을 70% 진행시킨다 (쿨 22초)",
        _ => string.Empty,
    };
}

/// 캐릭터 한 종의 정의 (기획서 9장).
///
/// 캐릭터 하나 = 몬스터 컨셉 + 낮 액티브 1 + 밤 액티브 1 (9.1). 두 스킬을 같은 모양으로
/// 드는 이유는 화면이 둘을 같은 자리에 이름과 한 줄 설명으로 그리기 때문이다.
public readonly struct CharacterDef
{
    public readonly string Name;
    public readonly string DayName;
    public readonly string DayEffect;
    public readonly string NightName;
    public readonly string NightEffect;

    /// 외형 키. 모델·초상 표가 읽는다.
    public readonly CharacterId Id;
    public readonly DaySkill Day;
    public readonly NightSkill Night;

    public CharacterDef(string name, CharacterId id, NightSkill night, string nightName, string nightEffect)
    {
        Name = name;
        Id = id;
        Night = night;
        Day = DaySkills.Of(night);   // 9.1.1: 낮 액티브는 밤 액티브의 짝으로 정해진다
        DayName = DaySkills.NameOf(Day);
        DayEffect = DaySkills.EffectOf(Day);
        NightName = nightName;
        NightEffect = nightEffect;
    }
}

/// 캐릭터 5종 (기획서 9.1.1). 배열 인덱스가 곧 픽 번호다.
public static class CharacterCatalog
{
    public static readonly CharacterDef[] All =
    {
        new("도깨비",   CharacterId.Dokkaebi,  NightSkill.WillOWisp, "도깨비불", "가짜 아이템 박스를 설치한다 (쿨 18초)"),
        new("박쥐",     CharacterId.Bat,       NightSkill.Echo,      "메아리",   "주변의 안개를 즉시 걷어낸다 (쿨 18초)"),
        new("연금술사", CharacterId.Alchemist, NightSkill.Appraise,  "감별",     "박스의 가려진 슬롯이 즉시 공개된다 (쿨 16초)"),
        new("하운드",   CharacterId.Hound,     NightSkill.Track,     "추적",     "주변의 숨겨진 가방을 찾아낸다 (쿨 14초)"),
        new("미믹",     CharacterId.Mimic,     NightSkill.Illusion,  "환각",     "가짜로 숨겨진 가방을 심는다 (쿨 20초)"),
    };

    public static bool IsValid(int index) => index >= 0 && index < All.Length;

    /// 고르지 않은 상태. 팀 내 중복 픽 판정에서 "아무도 안 골랐다"와 구별해야 한다.
    public const int NoPick = -1;
}
