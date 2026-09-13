using TMPro;
using Unity.Netcode;
using UnityEngine;

/// 현재 구현된 캐릭터 스킬을 낮·밤 같은 자리에서 표시한다 (PDF 6쪽).
public sealed class UIDaySkillSlot : MonoBehaviour
{
    [SerializeField] UnityEngine.UI.Image icon;
    [SerializeField] UIRing cooldown;
    [SerializeField] TMP_Text label;
    [SerializeField] CharacterVisualConfig visuals;
    [SerializeField] AudioSource readySound;
    PlayerCharacter character;
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
            if (player != null) { character = player.GetComponent<PlayerCharacter>(); input = player.GetComponent<PlayerInputRouter>(); }
        }
        var visible = director != null && director.Phase.Current != Phase.Transition && character != null;
        group.alpha = visible ? 1f : 0f;
        if (!visible) { wasCooling = false; return; }
        var remaining = character.SkillCooldownRemaining;
        var duration = director.Phase.Current == Phase.Day ? DayBalance.SkillCooldown : NightSkills.CooldownOf(character.Skill);
        cooldown.Amount = duration > 0f ? 1f - Mathf.Clamp01(remaining / duration) : 1f;
        if (wasCooling && remaining <= 0f) { flashedAt = Time.unscaledTime; if (readySound != null && readySound.clip != null) readySound.Play(); }
        wasCooling = remaining > 0f;
        cooldown.color = Time.unscaledTime - flashedAt < .4f ? Color.white : new Color(1f, .8f, .35f);
        if (Time.unscaledTime < refreshAt) return;
        refreshAt = Time.unscaledTime + .1f;
        icon.sprite = character.HasPick ? visuals.IconFor(character.Def.Day) : null;
        icon.color = character.HasPick ? Color.white : Color.gray;
        label.text = character.HasPick ? $"{input?.SkillBinding}  {(remaining > 0f ? Mathf.CeilToInt(remaining).ToString() : "준비")}" : "미선택";
    }
}
