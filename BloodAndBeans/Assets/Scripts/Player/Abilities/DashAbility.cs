/// 대시 (기획서 6.6 · 11장 조작표). **캐릭터 픽과 무관하게 전원이 갖는 공통 액티브다.**
///
/// 돌진·넉백·넘어짐의 매 틱 물리와 피격 수신은 `DashHarass`가 그대로 든다 — 한 번 발동으로
/// 끝나지 않는 일이라 순수 객체로 내려올 수 없다. 여기로 온 것은 **발동 경로뿐이다.**
/// 게이트와 쿨타임을 낮·밤 액티브와 한 자리(`PlayerAbilities`)에서 보기 위한 것이다.
public sealed class DashAbility : ICommonAbility
{
    public bool TryCastServer(PlayerAbilities host) =>
        host.Dash != null && host.Dash.TryDashServer();

    /// 표가 아니라 컴포넌트가 든다. 대시는 스킬 표(`DaySkills`·`NightSkills`)에 없는
    /// 조작이고, 기획서 14장 #7이 아직 폭(5~8초)만 정했다.
    public float CooldownSeconds(PlayerAbilities host) =>
        host.Dash != null ? host.Dash.Cooldown : 0f;
}
