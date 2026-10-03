using Unity.Netcode;
using UnityEngine;

/// 현재 캐릭터 스킬의 쿨타임을 낮·밤 같은 자리에서 표시한다 (기획서 5.7.1).
/// 무엇을 보여 줄지만 정하고, 그리는 것은 원형 칸 파츠(`UISkillSlot`)가 한다.
public sealed class UIDaySkillSlot : MonoBehaviour
{
    [SerializeField] UISkillSlot slot;
    [Header("스킬 아이콘")]
    [SerializeField] Sprite igniteIcon;
    [SerializeField] Sprite glideIcon;
    [SerializeField] Sprite wispIcon;
    [SerializeField] Sprite echoIcon;
    [SerializeField] AudioSource readySound;
    PlayerCharacter character;
    PlayerAbilities abilities;
    PlayerInputRouter input;
    CanvasGroup group;
    float refreshAt;
    void Awake() => group = GetComponent<CanvasGroup>();
    void Update()
    {
        var director = MatchDirector.Instance;
        if (character == null)
        {
            var player = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            if (player != null) { character = player.GetComponent<PlayerCharacter>(); abilities = player.GetComponent<PlayerAbilities>(); input = player.GetComponent<PlayerInputRouter>(); }
        }
        var visible = director != null && director.Phase.Current != Phase.Transition && character != null && abilities != null;
        group.alpha = visible ? 1f : 0f;
        if (!visible) { slot.ResetCooldown(); return; }
        var remaining = abilities.CooldownRemaining;
        slot.SetIcon(character.HasPick ? IconFor(director.Phase.Current, character.DaySkill, character.Skill) : null);
        var duration = director.Phase.Current == Phase.Day ? DaySkills.CooldownOf(character.DaySkill) : NightSkills.CooldownOf(character.Skill);
        // 「각인」 보석으로 줄어든 실제 길이는 서버가 쿨타임을 걸 때 함께 준다 (기획서 8.2).
        if (remaining > 0f && abilities.CooldownDuration > 0f) duration = abilities.CooldownDuration;
        var ratio = character.HasPick && duration > 0f ? Mathf.Clamp01(remaining / duration) : 0f;
        if (slot.SetCooldown(ratio) && readySound != null && readySound.clip != null) readySound.Play();
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + .1f;
        slot.SetCount(character.HasPick && remaining > 0f ? Mathf.CeilToInt(remaining).ToString() : string.Empty);
        slot.SetKey(character.HasPick && input != null ? input.SkillBinding : string.Empty);
    }

    Sprite IconFor(Phase phase, DaySkill day, NightSkill night) => phase switch
    {
        Phase.Day => day switch
        {
            DaySkill.Ignite => igniteIcon,
            DaySkill.Glide => glideIcon,
            _ => null,
        },
        Phase.Night => night switch
        {
            NightSkill.WillOWisp => wispIcon,
            NightSkill.Echo => echoIcon,
            _ => null,
        },
        _ => null,
    };
}
