/// 기획서가 확정한 수치 한 벌. **여기 있는 값이 곧 폴백이다.**
///
/// 데이터 파일(`Assets/Data/*.csv`)이 없거나 파싱에 실패해도 게임은 이 값으로 그대로 돈다.
/// 데이터 기반 설계의 흔한 실패는 파일 하나가 깨지면 게임이 안 뜨는 것이라, 덮어쓰기로만
/// 동작하게 만든다. EditMode 테스트와 에디터 툴도 로더 없이 규칙을 그대로 돌릴 수 있다.
///
/// 필드 묶음은 `Assets/Data/`의 CSV 파일과 1:1로 맞춘다 — 로더가 파일 하나를 읽어 묶음
/// 하나를 채운다. 절 번호 주석은 기획서 대조의 근거이므로 값을 옮길 때 함께 옮긴다.
///
/// **enum은 여기 오지 않는다.** `Ingredient`·`MenuId`·`Gem`·`NightSkill` 같은 종류는
/// 코드가 `switch`로 분기하고 프리팹이 정수로 직렬화한 값이다. 데이터로 늘어나는 것은
/// 수치이지 종류가 아니다.
public sealed class BalanceData
{
    // --- rent.csv (기획서 3.2) ---

    /// 1~7일차 임대료. 표를 넘는 일차는 마지막 값으로 고정한다.
    public int[] RentByDay = { 50, 80, 120, 180, 260, 360, 480 };

    // --- penalty.csv (기획서 3.3) ---
    // 단계(None·1회·2회·3회)별 감소율. 보석과 같은 축에 마이너스로 붙는다.

    public float[] PenaltyCraftLoss = { 0f, 0.10f, 0.15f, 0.20f };   // 낮: 제작 속도
    public float[] PenaltyMoveLoss = { 0f, 0f, 0.10f, 0.20f };       // 낮: 이동 속도
    public float[] PenaltyVisionLoss = { 0f, 0.15f, 0.25f, 0.35f };  // 밤: 시야 반경
    public float[] PenaltyOpenLoss = { 0f, 0f, 0.20f, 0.30f };       // 밤: 박스 개봉 속도

    /// 감소를 다 먹여도 남는 최솟값.
    /// ponytail: 기획서 3.3에 하한이 없다. 같은 축에 붙는 감소가 늘면 그때 기획서로 올린다.
    public float PenaltyMinScale = 0.1f;

    // --- dayphases.csv (기획서 4장) ---
    // 7일 x (밤 120 + 낮 120 + 전환 10) = 1,750초, 약 29분 10초.

    public float NightSeconds = 120f;
    public float DaySeconds = 120f;
    public float TransitionSeconds = 10f;
    public int TotalDays = 7;

    // --- loadbands.csv (기획서 6.7) ---

    /// 밴드별 이동 속도 배수. 보간이 아니라 밴드 인덱스를 쓰는 이유는 임대료 페널티가
    /// 적재 단계를 정확히 한 칸 낮추기 때문이다 (기획서 3.3 밤 항목).
    public float[] LoadBandSpeed = { 1.00f, 0.92f, 0.80f, 0.55f, 0.30f, 0.10f, 0.01f };

    /// 밴드 상한. `ratio < LoadBandMax[i]`인 첫 i가 그 밴드이고, 전부 넘으면 마지막 밴드다.
    public float[] LoadBandMax = { 0.5f, 0.8f, 1.0f, 1.3f, 1.6f, 2.0f };

    /// 겉보기와 견제가 갈리는 선 (기획서 6.6). 두 규칙이 같은 수치를 쓰는 것이 요점이다.
    public float OverloadRatio = 0.8f;

    /// 화면 흔들림이 붙는 선 (기획서 6.7).
    public float ShakeRatio = 1.0f;

    /// 대시를 못 쓰게 되는 선 (기획서 6.6). 겉보기·낙하보다 10%p 낮다 — 견제 수단을 먼저
    /// 잃고 그다음에 표적이 되는 것이 의도된 순서다.
    public float DashBlockRatio = 0.7f;

    // --- night.csv (기획서 6장) ---

    public float BagCapacity = 10f;             // 6.7 가방 100% 기준선
    public float BoxOpenSeconds = 1.5f;         // 6.5.2 박스 개봉 홀드
    public float BagRetrieveSeconds = 1.5f;     // 6.7 묻은 가방 회수
    public float BagBurnSeconds = 3f;           // 6.7 소각
    public float DashSpillShare = 0.3f;         // 6.6 적재 80% 이상에게 대시하면 떨어지는 비율

    // --- day.csv (기획서 5.2 · 5.3 · 5.5 · 5.7.2 · 8.1) ---

    public float CoffeeSeconds = 2f;
    public float OvenSeconds = 4f;
    public float WashSeconds = 3f;
    public int Machines = 3;
    public int Sinks = 3;

    /// 5.5: 낮이 시작되면 이 간격으로 대기 슬롯 수만큼 들어오고, 그 뒤로는 자리가 비는 즉시.
    public float FirstEntrySeconds = 2f;

    /// 5.2: Perfect는 그 손님의 최대 인내심을 이 비율만큼 회복시킨다.
    public float PerfectPatienceRecovery = 0.15f;

    /// 5.7.2: 인내심 링의 촉박·위급 경계.
    public float PatienceWarning = 0.5f;
    public float PatienceUrgent = 0.2f;

    // --- gems.csv (기획서 8.1 · 8.2) ---

    /// 8.1: 6종 모두 3턴(낮 3회).
    public int GemTurns = 3;

    /// 순서는 `Gem` 열거자와 같다: 불씨·저울·바람·거품·각인·찻잎.
    public float[] GemScale = { 0.70f, 1.40f, 1.12f, 0.65f, 0.75f, 1.20f };
    public string[] GemNames = { "불씨", "저울", "바람", "거품", "각인", "찻잎" };
    public string[] GemEffects =
    {
        "조리 시간 -30%", "Perfect 폭 +40%", "이동 속도 +12%",
        "세척 시간 -35%", "액티브 쿨타임 -25%", "손님 인내심 +20%",
    };

    /// `Gem`을 재료로 바꾸는 표. 열거자 값이 연속이 아니라(WindGem=10 ... TeaGem=15)
    /// 캐스팅으로 대신할 수 없다.
    public Ingredient[] GemItems =
    {
        Ingredient.EmberGem, Ingredient.ScalesGem, Ingredient.WindGem,
        Ingredient.FoamGem, Ingredient.EngravingGem, Ingredient.TeaGem,
    };

    // --- gemchance.csv (기획서 6.5.2) ---
    // 1~7일차. 2등급 상자의 보석 확률, 3등급 상자의 블러드 빈 확률.

    public double[] Tier2GemChance = { 0.05, 0.05, 0.08, 0.10, 0.10, 0.12, 0.12 };
    public double[] Tier3BloodBeanChance = { 0, 0, 0, 0.30, 0.35, 0.45, 0.50 };

    // --- ingredients.csv (기획서 6.7 · 7.1) ---

    /// 무게. 순서는 `Ingredient` 열거자 0~9와 같다. 보석 전부가 `UpgradePart` 값을 쓴다.
    public float[] IngredientWeight = { 1.0f, 1.0f, 1.0f, 1.0f, 1.0f, 0.5f, 3.0f, 3.0f, 1.2f, 1.0f };

    /// 7.1 재료 표의 한글 표기. 열거자 순서와 같아야 한다.
    public string[] IngredientNames =
    {
        "우유", "크림", "초콜렛", "아몬드", "베리", "얼음",
        "블러드 빈", "업그레이드 재료", "원두", "빵 베이스",
    };

    // --- races.csv (기획서 5.5) ---

    /// 등장 분포: 좀비 30% · 늑대인간 10% · 나머지 15%씩. 5%가 한 칸이다.
    ///
    /// **이 순서가 결과를 바꾼다.** `Forecast.PickRace`가 이 순서로 만든 가방을 무작위
    /// 위치에서부터 훑어 처음 맞는 종족을 고른다. 열거자 순서로 다시 세우면 같은 시드가
    /// 다른 예보를 낸다.
    public (Race Race, int Share)[] RaceBagOrder =
    {
        (Race.Zombie, 6), (Race.Ghost, 3), (Race.Skeleton, 3),
        (Race.Werewolf, 2), (Race.Vampire, 3), (Race.Witch, 3),
    };

    /// 5.5 손님 종족 표. `Race` 열거자 순서와 같아야 한다.
    public string[] RaceNames = { "좀비", "뱀파이어", "유령", "해골", "늑대인간", "마녀" };

    // --- forecast.csv (기획서 5.5 · 5.6.1) ---

    /// 5.6.1: 1·2일차 2종, 3일차부터 3종.
    public int PopularCountEarly = 2;
    public int PopularCountLate = 3;
    public int PopularCountLateFromDay = 3;

    /// 5.5 규칙 3: 이 확률로 오늘 밤 풀에서, 나머지는 팀 보유 재료에서 뽑는다.
    public double ForecastPoolShare = 0.7;

    /// 5.5 규칙 4: 기본 메뉴(핫 아메리카노)는 주문의 1/이 값을 넘지 않는다.
    public int AmericanoCapDivisor = 5;

    // --- menus.csv (기획서 7.2) ---

    /// 기본가와 조합. 조합법 표시와 판매 정산이 같은 표를 쓴다.
    public MenuDef[] Menus =
    {
        new(MenuId.HotAmericano, 5, Ingredient.Bean),
        new(MenuId.IcedAmericano, 10, Ingredient.Bean, Ingredient.Ice),
        new(MenuId.CafeLatte, 11, Ingredient.Bean, Ingredient.Milk),
        new(MenuId.Einspanner, 16, Ingredient.Bean, Ingredient.Cream),
        new(MenuId.CafeMocha, 28, Ingredient.Bean, Ingredient.Milk, Ingredient.Chocolate),
        new(MenuId.IcedLatte, 24, Ingredient.Bean, Ingredient.Milk, Ingredient.Ice),
        new(MenuId.IcedMocha, 38, Ingredient.Bean, Ingredient.Milk, Ingredient.Chocolate, Ingredient.Ice),
        new(MenuId.ChocoBrownie, 19, Ingredient.BreadBase, Ingredient.Chocolate),
        new(MenuId.AlmondCookie, 17, Ingredient.BreadBase, Ingredient.Almond),
        new(MenuId.CreamCake, 21, Ingredient.BreadBase, Ingredient.Cream),
        new(MenuId.BerryTart, 23, Ingredient.BreadBase, Ingredient.Berry),
    };

    /// 7.2 메뉴 표의 한글 표기. `MenuId` 열거자 순서와 같아야 한다.
    public string[] MenuNames =
    {
        "핫 아메리카노", "아이스 아메리카노", "카페라떼", "아인슈페너", "카페모카", "아이스라떼",
        "초코 브라우니", "아몬드 쿠키", "크림 케이크", "베리 타르트", "아이스모카",
    };

    // --- saleprice.csv (기획서 5.2 · 5.6.2 · 1.4 · 7.2) ---

    /// 인기 재료 하나당 보너스. 곱하지 않고 합산해서 한 번만 적용한다 (5.6.2).
    public float PopularBonus = 0.30f;

    /// 1.4 · 7.2: 블러드 빈을 쓰면 커피 판매가 x3.
    public float BloodBeanMultiplier = 3f;

    /// 순서는 `Gauge` 열거자와 같다: Perfect·Good·Miss·Burnt. 5.2의 탄 것 x0.2 포함.
    public float[] GaugeMultiplier = { 1.3f, 1.0f, 0.7f, 0.2f };

    // --- lootslots.csv (기획서 6.5.2 · 6.5.5) ---

    /// 상자 하나가 담는 최대 *종류* 수. 개수 상한이 아니다.
    public int LootMaxTypes = 5;

    /// 등급이 정하는 슬롯 수 범위. 인덱스 0이 1등급이다. 고정이 아니라 범위인 것이
    /// 요점이다 — 고정이면 등급을 보는 순간 칸 수까지 알게 된다.
    public int[] LootSlotMin = { 2, 3, 4 };
    public int[] LootSlotMax = { 3, 4, 5 };

    // --- forest.csv (기획서 6.3 · 6.3.1) ---

    /// 구역 경계. 중심까지의 거리를 숲 반지름으로 나눈 값이다.
    /// ponytail: 기획서 6.3에 구역 반경이 없다. 레벨 디자인이 치수를 정하면 맵 데이터로 옮긴다.
    public float CoreRatio = 0.25f;
    public float MidRatio = 0.55f;

    /// 매 밤 총 개수 범위 (6.3.1). 일차와 무관하다.
    /// 6.3.1의 팀 수 비례(3팀 x1.5 · 4팀 x2)는 사용자 결정으로 적용하지 않는다.
    public int MinBoxes = 12;
    public int MaxBoxes = 16;

    /// 구역 배분 퍼센트. 순서는 `ForestRings.Zone`과 같다 (바깥 45 · 중간 35 · 중심 20).
    public int[] ZoneShares = { 45, 35, 20 };

    /// [구역][일차-1] = (1등급, 2등급, 3등급) 퍼센트 (6.3.1 「자리 x 일차」).
    public (int T1, int T2, int T3)[][] TierTable =
    {
        new[] { (100, 0, 0), (95, 5, 0), (90, 10, 0), (85, 15, 0), (80, 20, 0), (75, 25, 0), (70, 30, 0) },
        new[] { (70, 30, 0), (55, 45, 0), (40, 55, 5), (30, 60, 10), (25, 60, 15), (20, 60, 20), (15, 60, 25) },
        new[] { (30, 70, 0), (15, 75, 10), (10, 65, 25), (5, 55, 40), (0, 45, 55), (0, 35, 65), (0, 25, 75) },
    };

    // --- regen.csv (기획서 6.3.2 · 10장) ---

    /// 흔한 재료의 일차별 가중치. 0인 날에는 리젠되지 않는다 — 초콜렛은 3일차,
    /// 베리는 4일차부터다. 표에 없는 재료는 0이다.
    public (Ingredient Item, int[] ByDay)[] RegenWeights =
    {
        (Ingredient.Milk,      new[] { 35, 30, 25, 22, 20, 18, 18 }),
        (Ingredient.Ice,       new[] { 35, 28, 22, 20, 18, 17, 17 }),
        (Ingredient.Cream,     new[] { 30, 22, 18, 16, 15, 15, 15 }),
        (Ingredient.Almond,    new[] { 0, 20, 15, 13, 13, 13, 13 }),
        (Ingredient.Chocolate, new[] { 0, 0, 20, 17, 17, 18, 18 }),
        (Ingredient.Berry,     new[] { 0, 0, 0, 12, 17, 19, 19 }),
    };

    /// 맵별 흔한 재료 (10장 첫 줄: "맵마다 리젠되는 재료 타입이 정해져 있다").
    /// 기본 맵은 7.1 「숲에서 캐는 재료」에서 중심부 보상 둘을 뺀 나머지다.
    public (string MapId, Ingredient[] Pool)[] RegenMaps =
    {
        ("berry-grove", new[] { Ingredient.Milk, Ingredient.Cream, Ingredient.Berry, Ingredient.Ice }),
        ("default", new[]
        {
            Ingredient.Milk, Ingredient.Cream, Ingredient.Chocolate,
            Ingredient.Almond, Ingredient.Berry, Ingredient.Ice,
        }),
    };

    // --- skills.csv (기획서 9.1.2 · 9.1.3 · 9.2) ---

    /// 낮 액티브 쿨타임 (9.1.3 표). 순서는 `DaySkill` 열거자와 같고 [0]은 None이다.
    public float[] DaySkillCooldown = { 0f, 15f, 22f, 18f, 14f, 22f };

    /// 밤 액티브 쿨타임 (9.2 표). 순서는 `NightSkill` 열거자와 같고 [0]은 None이다.
    public float[] NightSkillCooldown = { 0f, 18f, 18f, 16f, 14f, 20f };

    /// 9.1.3 표의 지속.
    public float GlideSeconds = 3f;
    public float ShortcutSeconds = 2.5f;

    /// 9.1.2 삼키기: 세척 70% 진행이라 개수대 점유 3초가 0.9초가 된다.
    public float SwallowProgress = 0.7f;

    // ponytail: 9.1.2는 "크게 오른다"뿐이고 폭은 9.1의 +25~40%다. 상한으로 박았다.
    public float GlideSpeed = 1.4f;

    // ponytail: 9.1.2 불붙이기에 수치가 없다. 9.1 폭 상한 40% · 과열 3초로 박았다.
    public float IgniteCut = 0.4f;
    public float OverheatSeconds = 3f;

    /// 밤 액티브의 판정 반경과 지속 (9.2).
    /// ponytail: 기본 시야 12m(6.1)와 메아리 7m(9.2)가 충돌해 기존 20m를 유지한다.
    /// 기획 반경이 정리되면 확정 수치로 바꾼다.
    public float EchoRadius = 20f;
    public float AppraiseRadius = 8f;
    public float TrackRadius = 18f;
    public float TrackRevealSeconds = 6f;

    public string[] DaySkillNames = { "없음", "불붙이기", "활공", "정제", "지름길", "삼키기" };
    public string[] DaySkillEffects =
    {
        "",
        "쓰고 있는 설비의 남은 조리 시간을 줄인다. 놓은 뒤 설비가 잠깐 달아오른다 (쿨 15초)",
        "3초 동안 이동속도가 크게 오른다 (쿨 22초)",
        "다음 한 잔의 완성 게이지가 Perfect로 확정된다 (쿨 18초)",
        "2.5초 동안 다른 캐릭터를 통과한다 (쿨 14초)",
        "들고 있는 더러운 식기의 세척을 70% 진행시킨다 (쿨 22초)",
    };

    // --- characters.csv (기획서 9.1.1) ---

    /// 배열 인덱스가 곧 픽 번호다. 낮 액티브는 밤 액티브의 짝으로 정해지므로(9.1.1)
    /// 여기 적지 않는다.
    public (string Name, CharacterId Id, NightSkill Night, string NightName, string NightEffect)[] Characters =
    {
        ("도깨비",   CharacterId.Dokkaebi,  NightSkill.WillOWisp, "도깨비불", "가짜 아이템 박스를 설치한다 (쿨 18초)"),
        ("박쥐",     CharacterId.Bat,       NightSkill.Echo,      "메아리",   "주변의 안개를 즉시 걷어낸다 (쿨 18초)"),
        ("연금술사", CharacterId.Alchemist, NightSkill.Appraise,  "감별",     "박스의 가려진 슬롯이 즉시 공개된다 (쿨 16초)"),
        ("하운드",   CharacterId.Hound,     NightSkill.Track,     "추적",     "주변의 숨겨진 가방을 찾아낸다 (쿨 14초)"),
        ("미믹",     CharacterId.Mimic,     NightSkill.Illusion,  "환각",     "가짜로 숨겨진 가방을 심는다 (쿨 20초)"),
    };

    /// 9.1.1: 낮 액티브는 밤 액티브의 짝이다. 순서는 `NightSkill` 열거자와 같다.
    public DaySkill[] DaySkillOfNight =
    {
        DaySkill.None, DaySkill.Ignite, DaySkill.Glide,
        DaySkill.Refine, DaySkill.Shortcut, DaySkill.Swallow,
    };
}

/// 지금 적용 중인 수치. 규칙 클래스들이 여기를 읽는다.
///
/// **호출부는 이 타입을 몰라도 된다.** `Rent.Due(day)`·`Gems.CookTimeScale`처럼 기존
/// 정적 API가 그대로 남아 있고 본문만 여기를 거친다.
public static class Balance
{
    /// 기본값은 기획서 확정치 그대로다. 로더가 돌지 않아도 이 값으로 게임이 성립한다.
    public static BalanceData Current { get; private set; } = new();

    /// 데이터를 싣는다. 부팅 때 한 번만 부르고, `null`이면 폴백으로 되돌린다.
    public static void Load(BalanceData data) => Current = data ?? new();

    /// 폴백으로 되돌린다. 도메인 리로드를 끈 채 재생하면 이전 판의 데이터가 그대로
    /// 남으므로, 진입점이 이것을 부른다 (`FogOfWar.ResetShared`와 같은 이유).
    public static void Reset() => Current = new();

    /// 표를 넘는 일차는 마지막 값으로 고정한다 (기획서 3.2 `Rent.Due`가 세운 규칙을
    /// 일차 표 전부가 따른다). 일차는 1부터 센다.
    public static T ByDay<T>(T[] table, int day) =>
        table[System.Math.Min(System.Math.Max(day, 1), table.Length) - 1];
}

/// 표에서 파생시킨 값을 캐시한다. 데이터가 바뀌면 다시 만든다.
///
/// 접근할 때마다 새로 만들면 매 프레임 할당이 된다 — `PlayerCharacter.Def`가
/// `CharacterCatalog.All`을 프로퍼티에서 읽고 HUD가 그것을 매 프레임 탄다.
/// `Balance.Current`는 `Load`/`Reset`에서만 바뀌므로 참조 비교로 충분하다.
public sealed class Derived<T>
{
    readonly System.Func<BalanceData, T> build;
    BalanceData builtFrom;
    T value;

    public Derived(System.Func<BalanceData, T> build) => this.build = build;

    public T Value
    {
        get
        {
            var now = Balance.Current;
            if (!ReferenceEquals(builtFrom, now))
            {
                value = build(now);
                builtFrom = now;
            }
            return value;
        }
    }
}
