/// 연출 하나를 가리키는 id. **서버가 고르고 클라이언트가 그린다.**
///
/// 능력이 파티클 프리팹을 들지 않는 이유가 이것이다. 프리팹을 능력이 들면 캐릭터 프리팹마다
/// 파티클을 꽂아야 하고(옛 `BatSkillVisuals`·`DokkaebiSkillVisuals`가 그랬다), 같은 연출을
/// 두 능력이 쓰면 두 벌이 된다. id만 실어 보내면 **무엇을 그릴지는 표 한 곳**이 정한다
/// (`EffectManager`).
///
/// 값을 옮기지 않는다. 표가 정수로 이 값을 물고 있다.
public enum EffectId
{
    None = 0,

    /// 메아리 — 안개가 걷히는 자리 (기획서 9.2).
    Echo = 1,

    /// 불붙이기 — 설비에 붙는 불씨 (기획서 9.1.2).
    Ignite = 2,

    /// 도깨비불 — 가짜 상자가 서는 자리. 설치 팀에만 보인다 (기획서 9.2).
    Wisp = 3,

    /// 활공 — 캐릭터에 붙어 지속되는 연출 (기획서 9.1.2).
    Glide = 4,

    /// 대시가 맞은 자리 (기획서 6.6). 액티브는 아니지만 **같은 표에서 꺼낸다** —
    /// 연출을 어디에 두는가는 그것이 능력인지와 상관이 없다.
    DashHit = 5,

    /// 대시로 재료가 쏟아진 자리. 밀리기만 한 것과 다른 사건이라 연출도 갈린다 (6.6).
    DashSpill = 6,

    /// 상호작용 성공 — 노란 오각별. 서버가 아니라 소유자 자신이 결정한다
    /// (`PlayerController.InteractionSucceeded`, `SuccessRpc`는 소유자에게만 온다).
    InteractionSuccess = 7,

    /// 귀환·페이즈 이동의 출발과 도착 연출.
    TeleportDeparture = 8,
    TeleportArrival = 9,

    /// 제작 판정 — 머신에서 터지는 팀 전용 피드백 (5.2, DF-03).
    CraftingPerfect = 10,
    CraftingGood = 11,
    CraftingMiss = 12,
    CraftingBurnt = 13,
    /// 재료 조합 결과 — 탁한 연기와 금빛 반짝임 (5.7.3, DF-07·DF-24).
    Spoiled = 14,
    MenuReady = 15,

    /// Perfect 판매 — 손님 중심에서 퍼지는 금빛 원형 (DF-02).
    SalePerfect = 16,

    /// 공용 설비의 작동과 세척 완료 (DF-26·DF-30).
    CoffeeSteam = 17,
    OvenHeat = 18,
    WashFoam = 19,
    WashComplete = 20,
}

