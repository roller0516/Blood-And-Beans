using System;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 칸 그리드 창 (기획서 6.5.4·6.5.5). 무엇을 그릴지는 `ILootGrid`가 정한다 — 숲의
/// 상자, 견제로 흘린 재료, 쏟은 배낭, 그리고 낮의 재료 칸이 같은 창을 쓴다 (6.5.4).
///
/// 상자로 열렸을 때는 개봉 게이지가 다 차면 열리고, 이동하거나 맞아서 서버가 세션을
/// 닫을 때까지 떠 있는다.
///
/// 칸은 종류 기준 5개다. 열린 직후에는 일부가 `?`로 가려져 있고 **1초 간격으로 하나씩**
/// 정체를 드러낸다 (6.5.5). 드러난 칸을 누르면 통째로 가방에 들어가고, 아이콘이 HUD의
/// 가방으로 날아가 빨려 들어간다 (6.5.5의 DoTween 연출).
///
/// **트리는 프리팹에 있다.** 칸 5개는 기획서 6.5.5가 정한 상자의 상한이라 프리팹에
/// 그대로 깔려 있다. 재료 칸은 선반이 내주는 재료 수만큼이라 상한이 없어서, 모자라는
/// 만큼만 첫 칸을 복제해 더 깐다 (`SlotOf`). 자리와 창 너비는 `Panel`의
/// HorizontalLayoutGroup과 ContentSizeFitter가 잡는다 — 좌표를 코드로 계산하지 않는다.
///
/// `UI_목업.pptx` 4번은 가려진 슬롯을 **동시에** 공개하고 등급별로 시간을 다르게(0.6/1.0/
/// 1.4초) 그렸는데, 기획서 6.5.5는 1초 간격 순차다. 기획서를 따른다.
///
/// ponytail: 공개 진행도는 서버에서 따로 받지 않는다. 개봉 시각 하나만 복제되고 공개
/// 여부는 서버와 같은 식(`LootSlots.RevealedCount`)으로 클라이언트가 계산한다. 칸이
/// 드러날 때마다 RPC를 보내면 5칸에 RPC 5개다.
public sealed class UIBoxLootPopup : UIPopup
{
    [Serializable] struct IngredientIcon
    {
        public Ingredient Item;
        public Sprite Sprite;
    }

    [Header("머리")]
    [SerializeField] TMP_Text tierLabel;
    [SerializeField] TMP_Text title;
    [SerializeField] TMP_Text revealValue;

    [Header("가방")]
    [SerializeField] TMP_Text bagPercent;
    [SerializeField] TMP_Text bagWeight;

    /// 칸. 부품 배선과 색은 칸 프리팹이 들고 있어서 화면은 칸 하나씩만 잇는다
    /// (`UIBoxLootSlot`). 개수는 기획서 6.5.5가 정한 상한 5개라 프리팹에 그대로 깔려 있다.
    [Header("칸")]
    [SerializeField] UIBoxLootSlot[] slots = Array.Empty<UIBoxLootSlot>();

    /// 칸 사이의 금색 구분 띠. 칸이 하나도 없으면 띠만 남아 통짜 금색으로 보인다.
    [SerializeField] GameObject slotStrip;

    [Header("바닥")]
    [SerializeField] TMP_Text warning;

    [Header("연출")]
    [SerializeField] float flySeconds = 0.45f;

    /// 희귀 재료를 담을 때 남는 발광 꼬리(`BB/UI Glow Trail`). 흔한 재료에는 남기지
    /// 않는다 — 담을 때마다 날리면 희귀라는 신호가 흔해진다.
    ///
    /// 꼬리 모양은 셰이더가 UV로 그린다. 쿼드는 **한 장**이고 매 프레임 출발점과 머리를
    /// 잇도록 늘어난다. `TrailRenderer`는 월드 렌더러라 Overlay 캔버스에서 그려지지
    /// 않고, 사본을 잘게 떨구면 비행 한 번에 GameObject가 열댓 개 생겼다 사라진다.
    [SerializeField] Material trailMaterial;

    /// ponytail: 두께·길이·밝기는 눈으로 맞춘 임시값이다. 기획서에 연출 표가 없다.
    [SerializeField] float trailThickness = 26f;
    [SerializeField] float trailMaxLength = 220f;
    [SerializeField] float trailFadeSeconds = 0.18f;
    [SerializeField, Range(0f, 1f)] float trailAlpha = 0.85f;
    [SerializeField] float bagPunchScale = 0.35f;
    [SerializeField] float bagPunchSeconds = 0.3f;

    /// 가려진 칸에 뜨는 남은 시간 안내. 공개 간격은 `ItemBox.revealInterval`이 정한다.
    const string RevealHint = "IN 1s";

    /// 재료 아이콘. 비워 두면 이름 글자로만 그린다 — 아트가 붙기 전에도 규칙은 확인된다.
    [SerializeField] IngredientIcon[] icons = Array.Empty<IngredientIcon>();

    /// 날아가는 사본이 붙을 곳. 칸보다 위에 그려져야 해서 프리팹에 따로 둔다.
    [SerializeField] RectTransform flyLayer;

    /// 지금 그리는 그리드. 상자일 수도 재료 칸일 수도 있다 (기획서 6.5.4).
    ILootGrid source;

    /// 칸을 눌렀을 때 부를 곳. 상자는 소유자 검증을 받는 `PlayerInteract`를 거치고
    /// 재료 칸은 자기 RPC로 보내므로, 어디로 갈지는 창을 여는 쪽이 정한다.
    Action<int> take;

    RectTransform bagAnchor;

    /// 프리팹에 깔린 칸이 모자랄 때 더 깐 것들. 첫 칸의 복제라 배선이 그대로 따라온다.
    readonly List<UIBoxLootSlot> grown = new();

    /// 로컬 플레이어의 가방. 묻어 두면 서버가 담기를 전부 거절하는데(`PlayerInventory.
    /// AddServer`), 그 사실이 화면에 없으면 눌러도 아무 일이 없는 창으로만 보인다.
    PlayerInventory bag;

    /// 가방 아이콘의 원래 크기. 펀치가 겹치면 DOTween이 부풀어 있는 값을 다음 펀치의
    /// 시작값으로 잡아, 담을 때마다 아이콘이 조금씩 커진 채로 남는다.
    Vector3 bagScale = Vector3.one;

    int lastRevealed = -1;
    int lastSignature = -1;

    protected override void Awake()
    {
        base.Awake();
        for (var i = 0; i < slots.Length; i++) Arm(slots[i], i);
    }

    /// `MatchFlow`가 그릴 그리드와 칸을 눌렀을 때 부를 곳을 넘겨 준다. `anchor`는
    /// 아이템이 빨려 들어갈 HUD의 가방 아이콘이며 없으면 연출만 생략된다. `carrier`는
    /// 밤의 가방이고, 낮의 재료 칸처럼 가방과 무관한 그리드에서는 비어 있다.
    public void Bind(ILootGrid value, Action<int> onTake, PlayerInventory carrier,
                     RectTransform anchor)
    {
        source = value;
        take = onTake;
        bag = carrier;
        bagAnchor = anchor;
        if (anchor != null) bagScale = anchor.localScale;
        lastRevealed = -1;
        lastSignature = -1;
    }

    public override void OnHide()
    {
        for (var i = 0; i < slots.Length; i++)
            if (slots[i] != null) slots[i].HideTooltip();
        for (var i = 0; i < grown.Count; i++)
            if (grown[i] != null) grown[i].HideTooltip();

        source = null;
        take = null;
        bagAnchor = null;
        bag = null;
    }

    void Update()
    {
        if (source == null) return;

        var revealed = source.RevealedCount;
        var signature = Signature();
        if (revealed == lastRevealed && signature == lastSignature) return;

        // 연출은 "이번에 새로 열린 칸"에만 건다. 덮어쓰기 전에 직전 값을 넘긴다.
        var previous = lastRevealed;
        lastRevealed = revealed;
        lastSignature = signature;
        Render(revealed, previous);
    }

    /// 내용물이 바뀌었는지만 알면 된다. 남의 손에 칸이 비면 이 값이 달라진다.
    int Signature()
    {
        var hash = source.SlotCount * 2 + (HasBag ? 1 : 0);
        for (var i = 0; i < source.SlotCount; i++)
            hash = hash * 397 ^ ((int)source.SlotItem(i) * 31 + source.SlotCountAt(i));
        return hash;
    }

    /// 가방을 메고 있는가. 참조가 없으면(연출 생략 상태) 막지 않는다 — 담기 판정은
    /// 어차피 서버가 한다.
    bool HasBag => bag == null || bag.HasBag;

    /// `previousRevealed`가 음수면 창을 방금 연 것이다. 그때는 이미 열려 있던 칸이
    /// 한꺼번에 터지므로 연출을 걸지 않는다 — 남이 먼저 연 상자를 늦게 열었을 때다.
    void Render(int revealed, int previousRevealed)
    {
        var slotCount = source.SlotCount;
        var remaining = 0;
        for (var i = 0; i < slotCount; i++) if (source.SlotCountAt(i) != 0) remaining++;

        var hidden = Mathf.Max(0, slotCount - revealed);
        Set(tierLabel, source.GridTitle);
        Set(title, !HasBag
            ? "가방을 묻어 뒀다 — 담을 수 없다"
            : $"슬롯 {slotCount} · 남은 것 {remaining}");
        Set(revealValue, hidden > 0 ? $"{hidden}칸 공개 중…" : "전부 공개됨");

        // 개인 인벤토리는 없고 무게만 있다 (기획서 6.5.6).
        if (bag != null)
        {
            Set(bagPercent, $"{Mathf.RoundToInt(bag.LoadRatio * 100f)}%");
            Set(bagWeight, $"{bag.Carried:0.0} KG");
        }

        Set(warning, source.GridHint);

        if (slotStrip != null) slotStrip.SetActive(slotCount > 0);

        // 프리팹의 칸과 더 깐 칸을 합쳐 훑는다. 남는 칸은 꺼 둬야 레이아웃이 창 너비를
        // 그만큼 줄인다.
        var shown = Mathf.Max(slotCount, slots.Length + grown.Count);
        for (var i = 0; i < shown; i++)
        {
            var slot = SlotOf(i);
            if (slot == null) continue;

            if (i >= slotCount) { slot.gameObject.SetActive(false); continue; }
            slot.gameObject.SetActive(true);

            var item = source.SlotItem(i);
            var count = source.SlotCountAt(i);

            if (i >= revealed) { slot.ShowHidden(RevealHint); continue; }
            if (count == 0) { slot.ShowEmpty(); continue; }

            // 음수는 무제한이다 (`ILootGrid.SlotCountAt`). 개수를 곱하면 무게가 음수가 된다.
            var rarity = Ingredients.RarityOf(item);
            slot.ShowItem(
                DisplayNames.Of(item),
                count < 0 ? "∞" : count > 1 ? $"×{count}" : string.Empty,
                $"{Ingredients.WeightOf(item) * Mathf.Max(count, 1):0.0} KG",
                SpriteOf(item),
                rarity);

            if (previousRevealed >= 0 && i >= previousRevealed
                && rarity == IngredientRarity.Rare)
                slot.PlayRareFlourish();
        }
    }

    /// `index`번째 칸. 프리팹에 깔린 것이 모자라면 첫 칸을 복제해 채운다 — 재료 칸은
    /// 선반이 내주는 재료 수만큼이라 상한이 기획서로 고정돼 있지 않다 (AGENTS.md
    /// 「에셋과 프로젝트 파일」). 자리는 레이아웃이 잡으므로 좌표를 손대지 않는다.
    UIBoxLootSlot SlotOf(int index)
    {
        if (index < slots.Length) return slots[index];
        if (slots.Length == 0 || slots[0] == null) return null;

        while (slots.Length + grown.Count <= index)
        {
            var clone = Instantiate(slots[0], slots[0].transform.parent);
            clone.name = $"Slot{slots.Length + grown.Count}";
            Arm(clone, slots.Length + grown.Count);
            grown.Add(clone);
        }
        return grown[index - slots.Length];
    }

    /// 칸에 자기 번호를 붙여 준다. 칸은 자기가 몇 번째인지 모른다 (`UIBoxLootSlot`).
    void Arm(UIBoxLootSlot slot, int index)
    {
        if (slot == null) return;
        slot.Bind(() => OnSlotClicked(index));
        slot.HideTooltip();
    }

    Sprite SpriteOf(Ingredient item)
    {
        for (var i = 0; i < icons.Length; i++)
            if (icons[i].Item == item) return icons[i].Sprite;
        return null;
    }

    /// 칸을 눌렀다. 실제로 담기는지는 서버가 정하므로 연출은 요청과 함께 바로 시작한다 —
    /// 왕복을 기다리면 누른 느낌이 사라진다. 서버가 거절하면 다음 갱신에서 칸이 그대로
    /// 남아 있는 것으로 드러난다.
    void OnSlotClicked(int index)
    {
        if (source == null || take == null || !HasBag) return;
        if (index >= source.RevealedCount || source.SlotCountAt(index) == 0) return;

        var rarity = Ingredients.RarityOf(source.SlotItem(index));
        take(index);
        FlyToBag(SlotOf(index), rarity);
    }

    /// 누른 칸의 사본을 가방 아이콘으로 날린다. 원본을 옮기면 칸 배치가 무너진다.
    void FlyToBag(UIBoxLootSlot slot, IngredientRarity rarity)
    {
        if (bagAnchor == null || flyLayer == null || slot == null) return;

        // 창이 닫히면 필드가 비므로 지역 변수로 잡아 둔다. 트윈은 창보다 오래 산다.
        var anchor = bagAnchor;
        var scale = bagScale;
        var from = slot.Rect;

        var ghost = new GameObject("Ghost", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)ghost.transform;
        rect.SetParent(flyLayer, false);
        rect.sizeDelta = from.rect.size;
        rect.position = from.position;

        var image = ghost.GetComponent<Image>();
        image.sprite = slot.IconSprite;
        image.color = image.sprite != null ? Color.white : slot.FrameColor;
        image.raycastTarget = false;

        // 꼬리는 희귀에만 붙인다.
        var origin = from.position;
        var streak = rarity == IngredientRarity.Rare && trailMaterial != null
            ? CreateStreak(slot.ColorOf(rarity))
            : null;

        DOTween.Sequence()
            .Append(rect.DOMove(anchor.position, flySeconds).SetEase(Ease.InQuad))
            .Join(rect.DOScale(0.2f, flySeconds).SetEase(Ease.InQuad))
            .OnUpdate(() =>
            {
                if (streak != null) StretchStreak(streak, origin, rect.position);
            })
            // 펀치는 시퀀스 밖에서 새로 나는 트윈이라 시퀀스의 SetUpdate를 물려받지 않는다.
            // 직접 걸지 않으면 시간 배율이 0일 때 아이템만 날아가고 가방은 반응하지 않는다.
            .AppendCallback(() =>
            {
                // 앞선 펀치를 끝내고 원래 크기로 되돌린 뒤에 친다. 겹쳐 치면 부풀어 있는
                // 값이 다음 펀치의 시작값이 돼서 아이콘이 영구히 커진다.
                anchor.DOKill();
                anchor.localScale = scale;
                anchor.DOPunchScale(Vector3.one * bagPunchScale, bagPunchSeconds, 1, 0.5f)
                    .SetUpdate(true);
            })
            .OnComplete(() =>
            {
                Destroy(ghost);
                if (streak == null) return;

                // 꼬리는 머리가 도착한 뒤에도 잠깐 남아 사라진다. 같이 지우면 자취가
                // 뚝 끊겨서 날아간 길이 안 읽힌다.
                FadeTo(streak, 0f, trailFadeSeconds)
                    .SetUpdate(true)
                    .OnComplete(() => Destroy(streak.gameObject));
            })
            .SetUpdate(true);       // 팝업이 떠 있는 동안 시간 배율이 어떻든 돈다
    }

    /// 꼬리 쿼드 한 장. 꼬리 끝(uv.x=0)에 자리를 잡고 머리 쪽으로 늘어나므로
    /// 피벗을 왼쪽 가운데에 둔다.
    Image CreateStreak(Color tint)
    {
        var go = new GameObject("Trail", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(flyLayer, false);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);

        var image = go.GetComponent<Image>();
        image.material = trailMaterial;
        image.raycastTarget = false;
        image.color = new Color(tint.r, tint.g, tint.b, trailAlpha);

        // 사본보다 뒤에 그린다. 앞에 두면 꼬리가 아이콘을 덮는다.
        rect.SetAsFirstSibling();
        return image;
    }

    /// 출발점과 머리를 잇도록 늘인다. 길이는 `trailMaxLength`에서 자른다 — 자르지
    /// 않으면 창 끝에서 가방까지 화면을 가로지르는 막대가 된다.
    void StretchStreak(Image streak, Vector3 origin, Vector3 head)
    {
        var rect = (RectTransform)streak.transform;
        var delta = head - origin;
        var length = Mathf.Min(delta.magnitude, trailMaxLength);
        if (length <= 0.01f) return;

        var direction = delta.normalized;
        rect.position = head - direction * length;
        rect.rotation = Quaternion.Euler(0f, 0f,
            Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        rect.sizeDelta = new Vector2(length, trailThickness);
    }


    /// 알파만 트윈한다. `Image.DOFade`는 DOTween의 UI 모듈에 있는데, 그 모듈이
    /// `Assets/Plugins/Demigiant/DOTween/Modules/`에 asmdef 없이 놓여 Assembly-CSharp로
    /// 들어간다. asmdef인 BB.Client는 그걸 참조할 수 없다 — 코어 DLL의 `DOTween.To`로 푼다.
    static Tween FadeTo(Graphic target, float alpha, float seconds) =>
        DOTween.To(() => target.color.a,
                   a => { var c = target.color; c.a = a; target.color = c; },
                   alpha, seconds);

    static void Set(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }
}
