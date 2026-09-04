/// 낮 패시브의 종류 (기획서 9.1). 발동 키가 없는 상시 능력이다.
///
/// 효과가 걸리는 자리가 제각각이라(이동·손님·설거지·손·게이지) 수치를 한 곳에 모으는
/// 대신 **누가 무엇을 가졌는가**만 여기서 정하고, 수치는 아래 `DayPassives`가 든다.
public enum DayPassive
{
    /// 잰걸음 — 이동속도 +25%.
    Swift,

    /// 인기 카페 — 모든 손님의 인내심 +30%. **팀 단위**로 걸린다.
    PopularCafe,

    /// 깔끔쟁이 — 설거지 속도 2배.
    Tidy,

    /// 양손잡이 — 재료를 한 번에 2개까지 들 수 있다.
    Ambidextrous,

    /// 얼음 장인 — `Temp.Cold` 메뉴는 완성 게이지가 항상 Perfect.
    IceMaster,

    /// 붙임성 — 매장의 첫 손님은 인내심이 닳지 않는다. **팀 단위**로 걸린다.
    Welcoming,

    /// 강심장 — 대기 손님이 3명 이상일 때 이동속도 +40%.
    Stouthearted,

    /// 제빵사 — 오븐이 탈 때까지의 유예가 10초 → 20초. **팀 단위**로 걸린다.
    Baker,
}

/// 밤 액티브 스킬 (기획서 9.2). 쿨타임이 길고 캐릭터당 하나다.
public enum NightSkill
{
    /// 아직 정해지지 않은 칸. 기획서 9.2의 후보가 낮 패시브 수보다 적다.
    None,

    /// 도깨비불 — 가짜 아이템 박스를 설치한다 (쿨 40초).
    WillOWisp,

    /// 메아리 — 주변 넓은 범위의 안개를 즉시 걷어낸다 (쿨 40초).
    Echo,

    /// 감별 — 박스의 가려진 슬롯이 즉시 공개된다 (쿨 35초).
    Appraise,

    /// 추적 — 주변의 숨겨진 가방을 찾아낸다 (쿨 30초).
    Track,

    /// 환각 — 가짜로 숨겨진 가방을 심는다 (쿨 45초).
    Illusion,
}

/// 낮 패시브의 수치 (기획서 9.1). 표는 기획서가 정했고 폭은 "+25~40% 수준"으로 못박혀 있다.
public static class DayPassives
{
    /// 잰걸음 (기획서 9.1: 이동속도 +25%).
    public const float SwiftSpeed = 1.25f;

    /// 인기 카페 (기획서 9.1: 모든 손님의 인내심 +30%).
    public const float PatienceBonus = 1.30f;

    /// 깔끔쟁이 (기획서 9.1: 설거지 속도 2배). 세척 *시간*을 나누는 값이다.
    public const float WashSpeed = 2f;

    /// 양손잡이 (기획서 9.1: 재료를 한 번에 2개까지).
    public const int AmbidextrousCarry = 2;

    /// 손이 기본으로 드는 개수. 기획서 5.1이 "재료를 옮기는 것"을 한 번에 하나로 두고 있다.
    public const int BaseCarry = 1;

    /// 강심장 (기획서 9.1: 대기 손님이 3명 이상일 때 이동속도 +40%).
    public const int StoutheartedQueue = 3;
    public const float StoutheartedSpeed = 1.40f;

    /// 제빵사 (기획서 9.1: 오븐이 탈 때까지의 유예가 10초 → 20초).
    /// 기본 유예는 기획서 5.2의 10초이고 `CompletionGauge.window`가 든다.
    public const float BakerWindow = 20f;

    /// 팀 전체에 걸리는 패시브인가. 자기 팀 누군가가 가졌으면 팀원 전부에게 걸린다.
    ///
    /// 「모든 손님의 인내심」·「매장의 첫 손님」·「오븐의 유예」는 사람이 아니라 *가게*의
    /// 성질이다. 한 명이 인기 카페를 골랐는데 짝꿍이 받는 손님만 성질이 급하면 그것은
    /// 기획서가 말한 능력이 아니다.
    public static bool IsTeamWide(DayPassive p) =>
        p == DayPassive.PopularCafe || p == DayPassive.Welcoming || p == DayPassive.Baker;
}

/// 밤 액티브의 쿨타임 (기획서 9.2 표).
public static class NightSkills
{
    public static float CooldownOf(NightSkill s) => s switch
    {
        NightSkill.WillOWisp => 40f,
        NightSkill.Echo => 40f,
        NightSkill.Appraise => 35f,
        NightSkill.Track => 30f,
        NightSkill.Illusion => 45f,
        _ => 0f,
    };

    public static bool Exists(NightSkill s) => s != NightSkill.None;
}

/// 캐릭터 한 종의 정의 (기획서 9장).
///
/// 낮은 발동 키가 없는 상시 패시브이고(9.1), 밤은 쿨타임이 긴 액티브 스킬이다(9.2).
/// 두 칸을 같은 모양(`이름` + `효과`)으로 들고 있는 이유는 화면이 둘을 같은 자리에
/// 같은 방식으로 그리기 때문이다 — 성격이 달라도 표시할 것은 이름과 한 줄 설명이다.
public readonly struct CharacterDef
{
    public readonly string Name;
    public readonly string DayName;
    public readonly string DayEffect;
    public readonly string NightName;
    public readonly string NightEffect;

    /// 실제로 걸리는 효과. 문구(`DayEffect`)는 사람이 읽고 이 둘은 코드가 읽는다.
    public readonly DayPassive Day;
    public readonly NightSkill Night;

    public CharacterDef(string name, string dayName, string dayEffect,
                        string nightName, string nightEffect,
                        DayPassive day, NightSkill night)
    {
        Name = name;
        DayName = dayName; DayEffect = dayEffect;
        NightName = nightName; NightEffect = nightEffect;
        Day = day; Night = night;
    }
}

/// 캐릭터 후보 목록. 낮 패시브 8종은 기획서 9.1 표 그대로이고,
/// 밤 액티브는 9.2 표에서 가져온다.
public static class CharacterCatalog
{
    // ponytail: 밤 액티브는 기획서 9.2 표에서 취소선(안개탄)을 뺀 5종뿐인데 낮 패시브는
    // 9.1에 8종이 있다. 없는 스킬을 지어내지 않고 남는 세 칸은 "미정"으로 둔다.
    // 캐릭터 종 수 자체가 14장 #10 미결이라, 종 수가 정해지면 이 표도 함께 맞춘다.
    const string Undecided = "미정";
    const string UndecidedNote = "밤 액티브 미정 (기획서 9.2 후보 5종 · 14장 #10)";

    public static readonly CharacterDef[] All =
    {
        new("잰걸음",   "잰걸음",   "이동속도 +25%",
                        "도깨비불", "가짜 아이템 박스를 설치한다 (쿨 40초)",
                        DayPassive.Swift, NightSkill.WillOWisp),

        new("인기 카페", "인기 카페", "모든 손님의 인내심 +30%",
                        "메아리",   "주변 넓은 범위의 안개를 즉시 걷어낸다 (쿨 40초)",
                        DayPassive.PopularCafe, NightSkill.Echo),

        new("깔끔쟁이", "깔끔쟁이", "설거지 속도 2배",
                        "감별",     "박스의 가려진 슬롯이 즉시 공개된다 (쿨 35초)",
                        DayPassive.Tidy, NightSkill.Appraise),

        new("양손잡이", "양손잡이", "재료를 한 번에 2개까지 들 수 있다",
                        "추적",     "주변의 숨겨진 가방을 찾아낸다 (쿨 30초)",
                        DayPassive.Ambidextrous, NightSkill.Track),

        new("얼음 장인", "얼음 장인", "Temp.Cold 메뉴는 완성 게이지가 항상 Perfect",
                        "환각",     "가짜로 숨겨진 가방을 심는다 (쿨 45초)",
                        DayPassive.IceMaster, NightSkill.Illusion),

        new("붙임성",   "붙임성",   "매장의 첫 손님은 인내심이 닳지 않는다",
                        Undecided,  UndecidedNote,
                        DayPassive.Welcoming, NightSkill.None),

        new("강심장",   "강심장",   "대기 손님이 3명 이상일 때 이동속도 +40%",
                        Undecided,  UndecidedNote,
                        DayPassive.Stouthearted, NightSkill.None),

        new("제빵사",   "제빵사",   "오븐이 탈 때까지의 유예가 10초 → 20초",
                        Undecided,  UndecidedNote,
                        DayPassive.Baker, NightSkill.None),
    };

    public static bool IsValid(int index) => index >= 0 && index < All.Length;

    /// 고르지 않은 상태. 팀 내 중복 픽 판정에서 "아무도 안 골랐다"와 구별해야 한다.
    public const int NoPick = -1;
}
