using TMPro;
using Unity.Netcode;
using UnityEngine;

/// 현재 캐릭터 스킬의 쿨타임을 낮·밤 같은 자리에서 표시한다 (기획서 5.7.1).
/// 원 안에는 남은 초만 뜨고, 게이지는 아래에서 위로 찬다. 방향은 프리팹의 Filled 설정이 정한다.
public sealed class UIDaySkillSlot : MonoBehaviour
{
    /// Filled · Vertical · Bottom 원 이미지.
    [SerializeField] UnityEngine.UI.Image cooldown;
    /// 원 안의 남은 초.
    [SerializeField] TMP_Text count;
    /// 원 아래의 스킬 키.
    [SerializeField] TMP_Text key;
    [SerializeField] AudioSource readySound;
    PlayerCharacter character;
    PlayerAbilities abilities;
    PlayerInputRouter input;
    CanvasGroup group;
    bool wasCooling;
    float flashedAt = -1f;
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
        if (!visible) { wasCooling = false; return; }
        var remaining = abilities.CooldownRemaining;
        var duration = director.Phase.Current == Phase.Day ? DaySkills.CooldownOf(character.DaySkill) : NightSkills.CooldownOf(character.Skill);
        // 「각인」 보석으로 줄어든 실제 길이는 서버가 쿨타임을 걸 때 함께 준다 (기획서 8.2).
        if (remaining > 0f && abilities.CooldownDuration > 0f) duration = abilities.CooldownDuration;
        cooldown.fillAmount = character.HasPick && duration > 0f ? 1f - Mathf.Clamp01(remaining / duration) : 0f;
        if (wasCooling && remaining <= 0f) { flashedAt = Time.unscaledTime; if (readySound != null && readySound.clip != null) readySound.Play(); }
        wasCooling = remaining > 0f;
        cooldown.color = Time.unscaledTime - flashedAt < .4f ? Color.white : new Color(1f, .8f, .35f, .85f);
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + .1f;
        count.text = character.HasPick && remaining > 0f ? Mathf.CeilToInt(remaining).ToString() : string.Empty;
        key.text = character.HasPick ? input?.SkillBinding : string.Empty;
    }
}
