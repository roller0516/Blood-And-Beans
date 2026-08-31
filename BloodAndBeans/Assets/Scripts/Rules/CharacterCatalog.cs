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

    public CharacterDef(string name, string dayName, string dayEffect,
                        string nightName, string nightEffect)
    {
        Name = name;
        DayName = dayName; DayEffect = dayEffect;
        NightName = nightName; NightEffect = nightEffect;
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
                        "도깨비불", "가짜 아이템 박스를 설치한다 (쿨 40초)"),
        new("인기 카페", "인기 카페", "모든 손님의 인내심 +30%",
                        "메아리",   "주변 넓은 범위의 안개를 즉시 걷어낸다 (쿨 40초)"),
        new("깔끔쟁이", "깔끔쟁이", "설거지 속도 2배",
                        "감별",     "박스의 가려진 슬롯이 즉시 공개된다 (쿨 35초)"),
        new("양손잡이", "양손잡이", "재료를 한 번에 2개까지 들 수 있다",
                        "추적",     "주변의 숨겨진 가방을 찾아낸다 (쿨 30초)"),
        new("얼음 장인", "얼음 장인", "Temp.Cold 메뉴는 완성 게이지가 항상 Perfect",
                        "환각",     "가짜로 숨겨진 가방을 심는다 (쿨 45초)"),
        new("붙임성",   "붙임성",   "매장의 첫 손님은 인내심이 닳지 않는다",
                        Undecided,  UndecidedNote),
        new("강심장",   "강심장",   "대기 손님이 3명 이상일 때 이동속도 +40%",
                        Undecided,  UndecidedNote),
        new("제빵사",   "제빵사",   "오븐이 탈 때까지의 유예가 10초 → 20초",
                        Undecided,  UndecidedNote),
    };
}
