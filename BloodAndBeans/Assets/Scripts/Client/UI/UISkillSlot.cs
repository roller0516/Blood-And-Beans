using TMPro;
using UnityEngine;

/// 원형 스킬 칸 하나. 아이콘·쿨타임 게이지·남은 초·아래 키 글자를 그린다.
/// 무엇을 보여 줄지는 담는 쪽(`UIDaySkillSlot`, `UIMatchHudScreen`)이 정한다.
public sealed class UISkillSlot : MonoBehaviour
{
    [SerializeField] UnityEngine.UI.Image icon;
    /// Filled · Radial360 · Top 원 이미지. 남은 몫만큼 아이콘을 어둡게 덮는다.
    [SerializeField] UnityEngine.UI.Image cooldown;
    /// 선택적인 별도 테두리. 배경 자체에 테두리가 있으면 비워 둔다.
    [SerializeField] UnityEngine.UI.Image ring;
    /// 원 안의 남은 초.
    [SerializeField] TMP_Text count;
    /// 원 아래의 키.
    [SerializeField] TMP_Text key;
    /// 쿨타임 동안 아이콘을 흐리게 해 남은 초가 읽히게 한다.
    [SerializeField, Range(0f, 1f)] float coolingIconAlpha = .35f;
    [SerializeField] Color ringColor = new(1f, 1f, 1f, .95f);
    [Header("준비 완료 VFX")]
    [SerializeField] ParticleSystem readyEffect;
    bool wasCooling;

    void Awake()
    {
        if (readyEffect == null)
            CDebug.LogError("UISkillSlot의 준비 완료 VFX를 프리팹에 연결해야 한다.", this);
    }

    void OnDisable()
    {
        if (Application.isPlaying) ResetCooldown();
    }

    public void SetIcon(Sprite sprite)
    {
        if (icon.sprite != sprite) { ResetCooldown(); icon.sprite = sprite; }
        icon.enabled = sprite != null;
    }

    public void SetKey(string text)
    {
        if (key.text != text) key.text = text;
    }

    public void SetCount(string text)
    {
        if (count.text != text) count.text = text;
    }

    /// 남은 쿨타임 비율(0이면 준비)을 그린다. 준비로 바뀐 순간이면 true를 돌려준다.
    public bool SetCooldown(float remaining01)
    {
        cooldown.fillAmount = remaining01;
        var cooling = remaining01 > 0f;
        var becameReady = wasCooling && !cooling;
        if (cooling && !wasCooling) ClearReadyEffect();
        wasCooling = cooling;
        icon.color = new Color(1f, 1f, 1f, cooling ? coolingIconAlpha : 1f);
        if (becameReady && icon.enabled)
            readyEffect.Play(true);
        return becameReady;
    }

    /// 칸이 감춰질 때 부른다. 다시 보일 때 준비 강조가 잘못 터지지 않게 한다.
    public void ResetCooldown()
    {
        wasCooling = false;
        cooldown.fillAmount = 0f;
        icon.color = Color.white;
        if (ring != null) ring.color = ringColor;
        ClearReadyEffect();
    }

    void ClearReadyEffect()
    {
        if (readyEffect.IsAlive(true))
            readyEffect.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }
}
