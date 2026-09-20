/// 캐릭터 액티브 하나 (기획서 9.1.2 · 9.2).
///
/// **컴포넌트가 아니라 순수 C# 객체다.** NGO가 스폰 뒤에 `NetworkBehaviour`를 못 붙이기
/// 때문에 한때 10종을 전부 플레이어 프리팹에 얹었는데, 그러면 모든 플레이어가 **남의 능력
/// 8개를 달고 다닌다.** 복제가 필요한 것은 라우터(`PlayerAbilities`)가 대신 들고, 능력은
/// 뽑은 캐릭터에 맞춰 런타임에 하나만 만든다.
///
/// 복제 슬롯을 라우터가 대신 들 수 있는 이유는 **플레이어당 액티브가 낮 1 · 밤 1뿐**이기
/// 때문이다. 지속 시각 하나와 1회성 플래그 하나면 활공·지름길·정제를 전부 덮는다.
///
/// **능력은 수치를 갖지 않는다.** 쿨다운·반경·지속은 전부 엑셀에서 온다
/// (`DaySkills`·`NightSkills` → `Balance.Current`).
public interface IPlayerAbility
{
    /// 발동. **false면 라우터가 쿨타임을 태우지 않는다** — 아무 일도 일어나지 않았는데
    /// 기다리게 하면 안 된다.
    bool TryCastServer(PlayerAbilities host);
}

/// 캐릭터와 무관하게 **전원이 갖는** 액티브 (대시). 낮·밤 픽처럼 갈리지 않으므로
/// `AbilityFactory`를 거치지 않고 라우터가 항상 하나 들고 있는다.
public interface ICommonAbility : IPlayerAbility
{
    /// 쿨타임(초). 낮·밤은 엑셀 표에서 오지만 공통 액티브는 표에 없어서 자기가 답한다.
    float CooldownSeconds(PlayerAbilities host);
}

public interface IDayAbility : IPlayerAbility
{
    DaySkill Id { get; }
}

public interface INightAbility : IPlayerAbility
{
    NightSkill Id { get; }
}

/// 라우터의 **지속 슬롯**(`DurationUntil`)을 쓰는 능력.
///
/// 슬롯은 전원에게 복제된다. 지름길처럼 피어마다 물리를 풀어야 하는 것이 있어서다 —
/// 서버에서만 끄면 소유자 예측이 막혀 화해가 당긴다.
public interface IDurationAbility
{
    /// 켜져 있는 동안 캐릭터에 붙는 연출. 없으면 `EffectId.None`.
    /// 화면은 이 값만 보고 그린다 — 클라이언트에 능력별 분기가 생기지 않는다.
    EffectId AttachedEffect { get; }

    /// 슬롯이 바뀌었다. **모든 피어에서 불린다.** 필요 없으면 비워 둔다.
    void OnDurationChanged(PlayerAbilities host, double until);
}
