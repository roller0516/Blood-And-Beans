using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// 상자 루팅 창의 칸 하나 (기획서 6.5.5). 프리팹은
/// `Assets/Prefabs/UI/Parts/UIBoxLootSlot.prefab`이고, 화면은 이 컴포넌트 하나만 잇는다.
///
/// 부품 배선과 색표를 칸이 들고 있는 이유는 칸이 여러 개이기 때문이다. 화면이 칸의
/// 자식을 직접 이으면 배선이 부품 수 × 칸 수로 불어나고, 칸을 하나 더 놓을 때마다
/// 그만큼을 손으로 다시 이어야 한다 (AGENTS.md 「에셋과 프로젝트 파일」).
///
/// 누름과 마우스 출입은 `Button`·`EventTrigger`를 얹지 않고 직접 받는다. 칸에는 이미
/// 이 컴포넌트가 있고, 그 둘은 같은 이벤트를 한 겹 더 돌려줄 뿐이다.
public sealed class UIBoxLootSlot : MonoBehaviour,
    IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    /// 가려진 칸의 이름 자리. 아직 무엇인지 모른다는 표시다.
    const string HiddenLabel = "— — —";

    [Header("부품")]
    [SerializeField] Image frame;
    [SerializeField] Image icon;

    [Header("가려짐")]
    [SerializeField] GameObject blind;
    [SerializeField] TMP_Text blindIn;

    /// 마우스를 올렸을 때만 뜨는 설명. 이름·개수·무게가 이 안에 들어 있어서, 평소에는
    /// 칸 그림만 남는다.
    [Header("설명")]
    [SerializeField] GameObject tooltip;
    [SerializeField] TMP_Text nameLabel;
    [SerializeField] TMP_Text rarityLabel;
    [SerializeField] TMP_Text countLabel;
    [SerializeField] TMP_Text weightLabel;

    /// 칸 얼굴에 늘 떠 있는 등급. 툴팁은 마우스를 올려야 뜨므로, 다섯 칸을 훑어보며
    /// 무엇이 희귀한지 고르는 데는 쓸 수 없다 (기획서 6.5.5의 상자는 잠깐 열려 있다).
    [SerializeField] TMP_Text rarityBadge;

    /// 등급 이름. 인덱스가 `IngredientRarity`다 — 색표와 같은 규칙이라 배열이 짧으면
    /// 그 등급은 빈 칸으로 남는다.
    ///
    /// 테두리 색만으로는 등급이 읽히지 않는다. 2px 링은 `IngredientRarity`가 두 값뿐이라
    /// 흔한 쪽과 바닥색의 차이가 작고, 무엇보다 "색이 이 뜻"이라는 것을 화면 어디서도
    /// 알려 주지 않는다. 글자는 그 자체로 읽힌다.
    [SerializeField] string[] rarityNames = { "일반", "희귀" };

    [Header("색")]
    /// 칸 바닥. **상태에 따라 바뀌지 않는다.** 예전에는 가려짐·담을 수 있음·비워짐마다
    /// 다른 색을 칠했는데, 칸 자체가 계속 색을 바꾸니 무엇이 상태 표시이고 무엇이
    /// 등급 표시인지 읽히지 않았다. 가려짐은 `blind` 덮개가, 비워짐은 아이콘이 없는
    /// 것이 이미 알려 준다.

    /// 등급이 없는 칸(가려짐·비워짐)의 테두리색.
    [SerializeField] Color slotColor = new(0.16f, 0.17f, 0.2f, 0.95f);

    [SerializeField] Color plainEdgeColor = Color.white;

    /// 공개된 칸의 테두리 등급 색. 인덱스가 `IngredientRarity`다 — 배열이 짧으면 그
    /// 등급은 `plainEdgeColor`로 남는다.
    ///
    /// 바닥이 아니라 테두리를 칠한다. 바닥은 고정이고, 움직이는 색은 등급 하나뿐이다.
    ///
    /// 희귀 색은 3등급 상자 머티리얼(`Box_T3.mat`)의 금보라 발광 (2.1, 1.35, 3.0)을
    /// 최대 성분으로 나눠 LDR로 내린 값이다 (기획서 6.5.2).
    /// 등급 글자만 따로 밝히는 색. 테두리 색은 발광 기준이라 어두워서 글자로는 잘
    /// 안 읽힌다 — 일반은 흰색으로 띄운다. **배열에 없는 등급은 테두리 색을 그대로
    /// 쓴다.** 그래서 희귀 색은 여기 없다 — 색표를 두 벌로 만들지 않기 위해서다.
    [SerializeField] Color[] rarityTextColors = { Color.white };

    [SerializeField] Color[] rarityEdgeColors =
    {
        new(0.45f, 0.50f, 0.58f, 1f),   // Common — 흰색보다 낮춰 희귀가 튀게 한다
        new(0.70f, 0.45f, 1.00f, 1f),   // Rare — 금보라
    };

    [Header("발광 프레임")]
    /// 칸을 감싸는 발광 코너 프레임(`BB/UI Glow Frame`). **등급을 나르는 것이 이것이다** —
    /// 예전에는 `Outline` 2px 링이 했는데, 링은 흔한 쪽과 바닥색의 차이가 작아 읽히지
    /// 않았다. 비워 두면 등급 글자만으로 동작한다.
    ///
    /// 색과 밝기를 정점 색(`Image.color`)으로 넘기는 이유는 uGUI가
    /// `MaterialPropertyBlock`을 받지 않아서다. 칸마다 머티리얼을 복제하면 칸 수만큼
    /// 머티리얼이 생기고 배칭이 깨진다.
    [SerializeField] Image glow;

    /// 칸을 두르는 정지 테두리. 예전 `Outline` 컴포넌트가 하던 일을 그대로 받는다 —
    /// 등급 색을 늘 띠고 있는 쪽은 이것이고, `glow`의 도는 조각은 그 위의 강조다.
    /// 같은 셰이더의 `_Ring` 모드를 쓴다. 비워 두면 조각만으로 동작한다.
    [SerializeField] Image edge;

    /// 평소 밝기. 알파가 곧 발광 세기다 — 1로 두면 다섯 칸이 전부 타오른다.
    [SerializeField, Range(0f, 1f)] float idleAlpha = 0.5f;

    /// 정지 테두리의 밝기. 조각보다 낮게 둔다 — 같으면 도는 것이 눈에 안 띈다.
    [SerializeField, Range(0f, 1f)] float edgeAlpha = 0.32f;

    /// ponytail: 밝기·시간·펀치는 눈으로 맞춘 임시값이다. 기획서에 연출 표가 없다.
    [SerializeField] float glowPunch = 0.35f;
    [SerializeField] float flourishSeconds = 0.5f;
    [SerializeField] float punchScale = 0.22f;

    /// 눌렀을 때 화면이 받아 갈 곳. 칸은 자기가 몇 번째인지 모른다.
    Action clicked;

    /// 펀치 전 원래 크기. 겹쳐 치면 DOTween이 부풀어 있는 값을 다음 펀치의 시작값으로
    /// 잡아 칸이 영구히 커진다 (`UIBoxLootPopup`의 가방 아이콘이 같은 것을 밟았다).
    Vector3 restScale = Vector3.one;

    /// 연출이 끝나고 돌아갈 색. 지금 칸이 무슨 상태인지를 그대로 들고 있다.
    Color idleGlow = Color.clear;

    /// 지금 담을 수 있는 칸인가. 설명을 띄울지도 이 값이 정한다.
    bool takable;

    /// 마우스가 칸 위에 있는가. 칸이 마우스 밑에서 정체를 드러내는 경우가 있어서
    /// 따로 든다 — 그때는 `PointerEnter`가 다시 오지 않으므로, 공개 시점에 이 값을
    /// 보고 설명을 열지 않으면 마우스를 뺐다 넣기 전까지 아무것도 뜨지 않는다.
    bool hovered;

    /// 날아가는 사본을 만들 때 화면이 읽는다.
    public RectTransform Rect => (RectTransform)transform;
    public Sprite IconSprite => icon != null ? icon.sprite : null;
    public Color FrameColor => frame != null ? frame.color : Color.white;

    public void Bind(Action onClick) => clicked = onClick;

    /// 등급 색을 밖에서도 읽는다. 날아가는 사본의 꼬리가 같은 색이어야 한다 —
    /// 색표가 두 벌이 되면 한쪽만 고쳐 놓고 다른 곳에서 다른 색이 나온다.
    public Color ColorOf(IngredientRarity rarity) => RarityEdge(rarity);


    /// 알파만 트윈한다. `Image.DOFade`는 DOTween의 UI 모듈에 있는데, 그 모듈이
    /// `Assets/Plugins/Demigiant/DOTween/Modules/`에 asmdef 없이 놓여 Assembly-CSharp로
    /// 들어간다. asmdef인 BB.Client는 그걸 참조할 수 없다 — 코어 DLL의 `DOTween.To`로 푼다.
    static Tween FadeTo(Graphic target, float alpha, float seconds) =>
        DOTween.To(() => target.color.a,
                   a => { var c = target.color; c.a = a; target.color = c; },
                   alpha, seconds);

    void Awake()
    {
        restScale = transform.localScale;
        if (frame != null) frame.color = slotColor;
        if (glow == null) return;

        glow.raycastTarget = false;
        glow.enabled = false;           // 켜는 것은 `Paint`가 등급을 보고 정한다
        if (edge != null) edge.raycastTarget = false;
    }

    /// 창이 내려가면 돌던 연출을 끊고 원래 크기로 되돌린다. 트윈은 창보다 오래 살아서,
    /// 놔두면 다음에 열 때 칸이 부푼 채이거나 잔광이 켜진 채로 남는다.
    void OnDisable()
    {
        transform.DOKill();
        transform.localScale = restScale;
        if (glow == null) return;

        glow.DOKill();
        glow.transform.DOKill();
        glow.transform.localScale = Vector3.one;
        glow.color = idleGlow;
    }

    /// 희귀가 공개된 순간의 연출. 흔한 재료에는 걸지 않는다 — 매번 터지면 희귀라는
    /// 신호 자체가 죽는다. "이번에 새로 공개됐고 희귀한가"는 화면이 판단한다.
    public void PlayRareFlourish()
    {
        // 앞선 펀치를 끝내고 원래 크기로 되돌린 뒤에 친다.
        transform.DOKill();
        transform.localScale = restScale;
        transform.DOPunchScale(Vector3.one * punchScale, flourishSeconds, 1, 0.5f)
                 .SetUpdate(true);

        if (glow == null) return;

        // 조각은 이미 `Paint`가 희귀 색으로 켜 뒀다. 여기서는 그 위에 한 번 치기만 한다.
        glow.enabled = true;
        glow.DOKill();
        glow.transform.DOKill();
        glow.transform.localScale = Vector3.one;
        glow.transform.DOPunchScale(Vector3.one * glowPunch, flourishSeconds, 1, 0.5f)
            .SetUpdate(true);

        glow.color = new Color(idleGlow.r, idleGlow.g, idleGlow.b, 1f);
        FadeTo(glow, idleAlpha, flourishSeconds).SetUpdate(true);
    }

    /// 아직 정체를 드러내지 않은 칸. `countdown`은 남은 시간 안내다.
    public void ShowHidden(string countdown)
    {
        if (blind != null) blind.SetActive(true);
        Set(blindIn, countdown);
        Set(nameLabel, HiddenLabel);
        Set(countLabel, string.Empty);
        Set(weightLabel, string.Empty);
        ClearRarity();
        SetIcon(null);
        Paint(plainEdgeColor, false);
    }

    /// 공개됐고 담을 수 있는 칸. 글자와 아이콘은 화면이 만든다 — 이름표와 아이콘 목록이
    /// 거기 있다.
    public void ShowItem(string name, string count, string weight, Sprite sprite,
                         IngredientRarity rarity)
    {
        if (blind != null) blind.SetActive(false);
        Set(blindIn, string.Empty);
        Set(nameLabel, name);
        Set(countLabel, count);
        Set(weightLabel, weight);
        SetRarity(rarity);
        SetIcon(sprite);
        Paint(RarityEdge(rarity), true, rarity == IngredientRarity.Rare);
    }

    /// 공개됐지만 남이 먼저 가져가 비워진 칸.
    public void ShowEmpty()
    {
        if (blind != null) blind.SetActive(false);
        Set(blindIn, string.Empty);
        Set(nameLabel, string.Empty);
        Set(countLabel, string.Empty);
        Set(weightLabel, string.Empty);
        ClearRarity();
        SetIcon(null);
        Paint(plainEdgeColor, false);
    }

    /// 창이 내려가는 순간에는 `PointerExit`가 오지 않는다. 남겨 두면 다음에 열 때 마우스를
    /// 올린 적도 없는 칸의 설명이 떠 있다.
    public void HideTooltip()
    {
        hovered = false;
        if (tooltip != null) tooltip.SetActive(false);
    }

    public void OnPointerClick(PointerEventData eventData) => clicked?.Invoke();

    public void OnPointerEnter(PointerEventData eventData)
    {
        hovered = true;
        SyncTooltip();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        hovered = false;
        SyncTooltip();
    }

    /// 설명은 마우스와 내용물이 둘 다 있을 때만 뜬다. 설명할 것이 없는 칸에는 설명도
    /// 없다 — 가려진 칸과 비워진 칸이 그렇다.
    void SyncTooltip()
    {
        if (tooltip != null) tooltip.SetActive(hovered && takable);
    }

    /// `accent`는 도는 조각을 켤지다. 정지 테두리는 이 값과 무관하게 늘 켜져 있다.
    /// 바닥은 여기서 건드리지 않는다 — `Awake`가 한 번 칠하고 그대로 둔다.
    void Paint(Color edgeColor, bool canTake, bool accent = false)
    {
        takable = canTake;
        SetGlow(edgeColor, accent);
        SyncTooltip();
    }

    /// 발광 프레임을 이 색으로 세운다. 돌던 연출은 끊는다 — 칸의 상태가 바뀐 뒤에도
    /// 이전 상태의 밝기로 타오르고 있으면 안 된다.
    void SetGlow(Color tint, bool accent)
    {
        idleGlow = new Color(tint.r, tint.g, tint.b, idleAlpha);

        // 정지 테두리는 연출과 무관하게 상태 색만 따른다. 여기에도 펀치를 걸면 칸이
        // 두 겹으로 흔들려 무엇이 강조인지 읽히지 않는다.
        if (edge != null) edge.color = new Color(tint.r, tint.g, tint.b, edgeAlpha);

        if (glow == null) return;

        glow.DOKill();
        glow.transform.DOKill();
        glow.transform.localScale = Vector3.one;
        glow.color = idleGlow;

        // 도는 조각은 희귀에만 붙인다. 흔한 재료까지 돌면 희귀라는 신호가 사라진다.
        // 알파 0으로 두지 않고 꺼 버리는 이유는, 투명해도 칸마다 드로우 콜이 남아서다.
        glow.enabled = accent;
    }

    /// 등급을 칸 얼굴과 툴팁 양쪽에 적는다. 색은 테두리와 같은 것을 쓴다 — 색과 글자가
    /// 같은 것을 가리켜야 링만 보이는 상황에서도 그 색이 무슨 뜻이었는지 이어진다.
    void SetRarity(IngredientRarity rarity)
    {
        var index = (int)rarity;
        var label = index < rarityNames.Length ? rarityNames[index] : string.Empty;
        var color = RarityText(rarity);

        Paint(rarityBadge, label, color);
        Paint(rarityLabel, label, color);
    }

    /// 등급 글자를 비운다. 가려진 칸과 비워진 칸에는 알려 줄 등급이 없다.
    void ClearRarity()
    {
        Set(rarityBadge, string.Empty);
        Set(rarityLabel, string.Empty);
    }

    static void Paint(TMP_Text target, string value, Color color)
    {
        if (target == null) return;
        target.text = value;
        target.color = color;
    }

    /// 글자에 쓸 등급 색. 배열에 없으면 테두리 색으로 떨어진다.
    Color RarityText(IngredientRarity rarity)
    {
        var index = (int)rarity;
        return index < rarityTextColors.Length ? rarityTextColors[index] : RarityEdge(rarity);
    }

    Color RarityEdge(IngredientRarity rarity)
    {
        var index = (int)rarity;
        return index < rarityEdgeColors.Length ? rarityEdgeColors[index] : plainEdgeColor;
    }

    void SetIcon(Sprite sprite)
    {
        if (icon == null) return;
        icon.sprite = sprite;
        icon.enabled = sprite != null;
    }

    static void Set(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }
}
