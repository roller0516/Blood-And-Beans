using System;
using DG.Tweening;
using TMPro;
using UnityEngine;

/// 낮 피드백 DF-03. 게임 판정을 계산하지 않고 서버의 최종 결과만 그린다.
public sealed class UICraftingJudgement : MonoBehaviour
{
    [Serializable]
    sealed class Style
    {
        public string text;
        public Color color;
        public float seconds;
        public float fontSize;
        public float rise;
        public float peakScale;
        public float tilt;
        public Material material;
        public EffectId effect;
    }

    [SerializeField] CanvasGroup group;
    [SerializeField] TMP_Text label;
    [SerializeField] RectTransform view;
    [SerializeField] TMP_Text[] echoes = Array.Empty<TMP_Text>();
    [SerializeField] Vector3 machineOffset = new(0f, 2.1f, 0f);
    [SerializeField] float echoSpread = 1.65f;
    [SerializeField] float echoAlpha = .5f;

    // DF-03의 문구·색·길이. 크기와 이동량은 아트 초깃값이며 프리팹에서 조정한다.
    // ponytail: 미정인 펀치·이동·회전은 기획 확정 때 이 프리팹의 Style을 조정한다.
    [SerializeField] Style[] styles =
    {
        new() { text = "Perfect!", color = new(1f, 200f/255f, 61f/255f), seconds = .7f, fontSize = 48f, rise = 42f, peakScale = 1.28f, tilt = -8f },
        new() { text = "Good!", color = new(1f, 246f/255f, 226f/255f), seconds = .5f, fontSize = 32f, rise = 18f, peakScale = 1.08f },
        new() { text = "Miss!", color = new(226f/255f, 69f/255f, 60f/255f), seconds = .5f, fontSize = 40f, rise = -28f, peakScale = 1.05f },
        new() { text = "Burnt!", color = new(226f/255f, 69f/255f, 60f/255f), seconds = .5f, fontSize = 40f, rise = -32f, peakScale = 1.05f, tilt = 5f },
    };

    Sequence animation;
    Vector2 labelOrigin;
    bool initialized;
    Vector3 machinePosition;
    bool followMachine;
    Camera cameraView;
    UIMatchHudScreen hud;

    public bool IsPlaying => animation != null && animation.IsActive();

    void Awake()
    {
        labelOrigin = label.rectTransform.anchoredPosition;
        group.interactable = group.blocksRaycasts = false;
        initialized = true;
        Clear();
    }

    public bool Play(Judgement judgement)
    {
        var index = (int)judgement;
        if (index < 0 || index >= styles.Length) return false;
        // Editor 프리뷰에서도 같은 연출을 사용한다.
        if (!initialized) Awake();
        // ponytail: 글자는 최신 판정 하나만 표시한다. 동시 판정 글자가 필요하면 HUD 파츠 수를 늘린다.
        Clear();
        var style = styles[index];
        label.text = style.text;
        label.color = Color.white;
        label.fontSharedMaterial = style.material;
        label.fontSize = style.fontSize;
        var rect = label.rectTransform;
        rect.anchoredPosition = labelOrigin;
        rect.localRotation = Quaternion.identity;
        rect.localScale = Vector3.one * .8f;
        group.alpha = 1f;
        var seconds = Mathf.Max(.01f, style.seconds);
        animation = DOTween.Sequence().SetUpdate(true).SetLink(gameObject);
        animation.Insert(0, rect.DOScale(style.peakScale, seconds * .12f).SetEase(Ease.OutBack));
        animation.Insert(seconds * .12f, rect.DOScale(1f, seconds * .18f).SetEase(Ease.OutSine));
        animation.Insert(0, rect.DOAnchorPosY(labelOrigin.y + style.rise, seconds).SetEase(Ease.OutCubic));
        if (style.tilt != 0f)
            animation.Insert(0, rect.DOPunchRotation(new Vector3(0, 0, style.tilt), seconds, 2, .25f));
        for (var i = 0; i < echoes.Length; i++)
        {
            var echo = echoes[i];
            echo.text = style.text;
            echo.fontSize = style.fontSize;
            echo.color = new Color(style.color.r, style.color.g, style.color.b, echoAlpha);
            echo.rectTransform.anchoredPosition = labelOrigin;
            echo.rectTransform.localScale = Vector3.one;
            var delay = i * seconds * .1f;
            var duration = seconds * .55f;
            animation.Insert(delay, echo.rectTransform.DOScale(echoSpread + i * .15f, duration).SetEase(Ease.OutQuad));
            animation.Insert(delay, echo.DOFade(0f, duration));
        }
        animation.Insert(seconds * .6f, group.DOFade(0f, seconds * .4f));
        animation.OnComplete(() => { animation = null; group.alpha = 0f; });
        return true;
    }

    public void PlayAt(Judgement judgement, Vector3 position)
    {
        if (!Play(judgement)) return;
        var effect = styles[(int)judgement].effect;
        if (effect != EffectId.None) EffectManager.Play(effect, position);
        machinePosition = position + machineOffset;
        followMachine = true;
        RefreshPosition();
    }

    /// 기존 HUD 표식 층으로 옮겨 머신을 따라간다. 게이지의 LateUpdate에서 갱신한다.
    public void RefreshPosition()
    {
        if (!IsPlaying || !followMachine) return;
        if (cameraView == null) cameraView = Camera.main;
        if (hud == null) UIManager.Instance.TryGet(out hud);
        if (cameraView != null && hud != null)
            view.gameObject.SetActive(hud.PlaceMarker(view, machinePosition, cameraView));
    }

    public void Clear()
    {
        animation?.Kill();
        animation = null;
        followMachine = false;
        if (group != null) group.alpha = 0f;
    }

    void OnDisable() => Clear();
    // 표식 층으로 이동한 그림도 소유자의 수명에 맞춰 정리한다.
    void OnDestroy() { if (view != null && view.parent != transform) Destroy(view.gameObject); }
}
