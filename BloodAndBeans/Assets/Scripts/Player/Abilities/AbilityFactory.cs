/// 뽑은 캐릭터에 맞는 능력 객체를 만든다 (기획서 9.1.1: 낮 1 · 밤 1).
///
/// **능력을 늘릴 때 고치는 곳은 여기 한 줄과 새 클래스뿐이다.** 라우터도 프리팹도 건드리지
/// 않는다. 표(`CharacterCatalog`)가 누가 무엇을 갖는지 정하고, 여기는 그 종류를 객체로 바꾼다.
///
/// 종류를 데이터로 두지 않는 이유는 `Balance.cs`가 적어 둔 그대로다 — **데이터로 늘어나는
/// 것은 수치이지 종류가 아니다.** 엑셀로 반경을 바꿀 수는 있어도 새 동작을 만들 수는 없다.
public static class AbilityFactory
{
    public static IDayAbility Day(DaySkill skill) => skill switch
    {
        DaySkill.Ignite => new IgniteAbility(),
        DaySkill.Glide => new GlideAbility(),
        DaySkill.Refine => new RefineAbility(),
        DaySkill.Shortcut => new ShortcutAbility(),
        DaySkill.Swallow => new SwallowAbility(),
        _ => null,
    };

    public static INightAbility Night(NightSkill skill) => skill switch
    {
        NightSkill.WillOWisp => new WispAbility(),
        NightSkill.Echo => new EchoAbility(),
        NightSkill.Appraise => new AppraiseAbility(),
        NightSkill.Track => new TrackAbility(),
        NightSkill.Illusion => new IllusionAbility(),
        _ => null,
    };
}
