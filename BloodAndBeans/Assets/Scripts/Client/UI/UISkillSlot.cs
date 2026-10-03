using DG.Tweening;
using TMPro;
using UnityEngine;

/// 원형 스킬 칸 하나. 아이콘·쿨타임 게이지·남은 초·아래 키 글자를 그린다.
/// 무엇을 보여 줄지는 담는 쪽(`UIDaySkillSlot`, `UIMatchHudScreen`)이 정한다.
public sealed class UISkillSlot : MonoBehaviour
{
    [SerializeField] UnityEngine.UI.Image icon;
    /// Filled · Radial360 · Top 원 이미지. 남은 몫만큼 아이콘을 어둡게 덮는다.
    [SerializeField] UnityEngine.UI.Image cooldown;
    /// 원 테두리. 준비되는 순간 잠깐 강조색으로 바뀐다.
    [SerializeField] UnityEngine.UI.Image ring;
    /// 원 안의 남은 초.
    [SerializeField] TMP_Text count;
    /// 원 아래의 키.
    [SerializeField] TMP_Text key;
    /// 쿨타임 동안 아이콘을 흐리게 해 남은 초가 읽히게 한다.
    [SerializeField, Range(0f, 1f)] float coolingIconAlpha = .35f;
    [SerializeField] Color ringColor = new(1f, 1f, 1f, .95f);
    [SerializeField] Color readyFlashColor = new(1f, .8f, .35f, 1f);
    [SerializeField] float readyFlashSeconds = .4f;
    [Header("준비 완료 — 번쩍임 · 확산 · 아이콘 팝")]
    [SerializeField] UnityEngine.UI.Image readyGlow;
    [SerializeField] UnityEngine.UI.Image readyBurst;
    [SerializeField, Min(1f)] float readyBurstScale = 1.35f;
    [SerializeField, Min(1f)] float readyIconScale = 1.15f;
    [SerializeField, Range(0f, 1f)] float readyGlowAlpha = .65f;
    bool wasCooling;
    float flashedAt = -1f;
    Sequence readyAnimation;

    void Awake()
    {
        if (readyGlow == null || readyBurst == null)
            CDebug.LogError("UISkillSlot의 ReadyGlow와 ReadyBurst를 프리팹에 연결해야 한다.", this);
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
        {
            flashedAt = Time.unscaledTime;
            PlayReadyEffect();
        }
        ring.color = flashedAt >= 0f && Time.unscaledTime - flashedAt < readyFlashSeconds ? readyFlashColor : ringColor;
        return becameReady;
    }

    /// 칸이 감춰질 때 부른다. 다시 보일 때 준비 강조가 잘못 터지지 않게 한다.
    public void ResetCooldown()
    {
        wasCooling = false;
        flashedAt = -1f;
        cooldown.fillAmount = 0f;
        icon.color = Color.white;
        ring.color = ringColor;
        ClearReadyEffect();
    }

    void PlayReadyEffect()
    {
        ClearReadyEffect();
        readyGlow.enabled = readyBurst.enabled = true;
        readyGlow.color = new Color(readyFlashColor.r, readyFlashColor.g, readyFlashColor.b, readyGlowAlpha);
        readyBurst.color = readyFlashColor;
        icon.rectTransform.localScale = Vector3.one * readyIconScale;
        readyAnimation = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        readyAnimation.Join(readyGlow.rectTransform.DOScale(readyBurstScale, readyFlashSeconds).SetEase(Ease.OutCubic));
        readyAnimation.Join(readyGlow.DOFade(0f, readyFlashSeconds));
        readyAnimation.Join(readyBurst.rectTransform.DOScale(readyBurstScale, readyFlashSeconds).SetEase(Ease.OutCubic));
        readyAnimation.Join(readyBurst.DOFade(0f, readyFlashSeconds).SetEase(Ease.InQuad));
        readyAnimation.Join(icon.rectTransform.DOScale(1f, readyFlashSeconds).SetEase(Ease.OutCubic));
        readyAnimation.OnComplete(() => { readyAnimation = null; ClearReadyEffect(); });
    }

    void ClearReadyEffect()
    {
        readyAnimation?.Kill();
        readyAnimation = null;
        readyGlow.enabled = readyBurst.enabled = false;
        readyGlow.rectTransform.localScale = readyBurst.rectTransform.localScale = Vector3.one;
        icon.rectTransform.localScale = Vector3.one;
    }
}
