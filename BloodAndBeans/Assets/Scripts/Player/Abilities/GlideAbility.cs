/// 활공 — 잠시 빨라진다 (기획서 9.1.2).
///
/// 속도를 여기서 밀지 않는다. 보석·미납 페널티와 **같은 축에 곱해져야** 하므로 합치는
/// 자리는 `PlayerCharacter.PushPassiveScaleServer` 한 곳이고, 여기는 켜져 있는지만 답한다.
public sealed class GlideAbility : IDayAbility, IDurationAbility
{
    public DaySkill Id => DaySkill.Glide;

    /// 켜져 있는 동안 캐릭터에 붙는다. 화면은 이 값만 보고 그린다.
    public EffectId AttachedEffect => EffectId.Glide;

    public bool TryCastServer(PlayerAbilities host)
    {
        host.SetDurationServer(DaySkills.GlideSeconds);
        return true;
    }

    /// 켜고 끄는 것은 복제된 슬롯이 알아서 한다. 여기서 할 일은 없다.
    public void OnDurationChanged(PlayerAbilities host, double until) { }
}
