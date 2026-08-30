/// 캐릭터 한 종의 정의 (기획서 9장).
///
/// 낮과 밤 능력을 같은 모양(`이름` + `효과`)으로 들고 있는 이유는 밤 슬롯의 성격이
/// 아직 갈려 있기 때문이다. 기획서 9.1은 밤 패시브 표를 전부 취소선 처리하고 "밤은
/// 액티브로 고정"이라고 적었고 9.2에 쿨타임 액티브 6종이 있는데, `UI_목업.pptx` 2번은
/// 아직 `NIGHT PASSIVE`로 그려져 있다. 어느 쪽으로 확정되든 표시할 것은 이름과 한 줄
/// 설명이라 화면은 손댈 필요가 없다.
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

/// 캐릭터 후보 목록. 낮 패시브 8종은 기획서 9.1 표 그대로다.
public static class CharacterCatalog
{
    // ponytail: 밤 칸은 기획서 9.1에서 취소선 처리된 밤 패시브 후보를 그대로 옮겨 뒀다.
    // 목업 2번이 그 상태를 그리고 있어 화면을 눈으로 대조할 수 있게 하려는 것이고,
    // 확정 사양이 아니다. 9.2 액티브로 확정되면 이 두 칸만 갈아 끼우면 된다.
    public static readonly CharacterDef[] All =
    {
        new("잰걸음",   "잰걸음",   "이동속도 +25%",
                        "손재주",   "박스 개봉·담기 속도 +40%"),
        new("인기 카페", "인기 카페", "모든 손님의 인내심 +30%",
                        "야행성",   "안개를 걷어내는 반경 +30%"),
        new("깔끔쟁이", "깔끔쟁이", "설거지 속도 2배",
                        "빠른 발",  "밤 이동속도 +25%"),
        new("양손잡이", "양손잡이", "재료를 한 번에 2개까지 들 수 있다",
                        "개코",     "가장 가까운 3등급 박스의 방향이 표시된다"),
        new("얼음 장인", "얼음 장인", "Temp.Cold 메뉴는 완성 게이지가 항상 Perfect",
                        "짐꾼",     "무게 감속 구간이 한 단계 유리하게 계산된다"),
        new("붙임성",   "붙임성",   "매장의 첫 손님은 인내심이 닳지 않는다",
                        "손버릇",   "박스의 가려진 슬롯 공개 시간이 절반으로 줄어든다"),
        new("강심장",   "강심장",   "대기 손님이 3명 이상일 때 이동속도 +40%",
                        "뚝심",     "대시에 맞았을 때 경직 시간 절반, 재료를 떨어뜨리지 않는다"),
        new("제빵사",   "제빵사",   "오븐이 탈 때까지의 유예가 10초 → 20초",
                        "메아리",   "주변 넓은 범위의 안개를 즉시 걷어낸다 (쿨 40초)"),
    };
}
