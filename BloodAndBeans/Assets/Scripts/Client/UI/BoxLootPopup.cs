using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 상자 루팅 창 (기획서 6.5.5). 개봉 게이지가 다 차면 열리고, 이동하거나 맞아서 서버가
/// 세션을 닫을 때까지 떠 있는다.
///
/// 칸은 종류 기준 5개다. 열린 직후에는 일부가 `?`로 가려져 있고 **1초 간격으로 하나씩**
/// 정체를 드러낸다 (6.5.5). 드러난 칸을 누르면 통째로 가방에 들어가고, 아이콘이 HUD의
/// 가방으로 날아가 빨려 들어간다 (6.5.5의 DoTween 연출).
///
/// **트리는 프리팹에 있다.** 이 클래스는 아무것도 만들지 않고 이어 둔 참조에 값만 넣는다.
/// 칸 5개는 기획서 6.5.5가 정한 상한이라 프리팹에 그대로 깔려 있다.
///
/// `UI_목업.pptx` 4번은 가려진 슬롯을 **동시에** 공개하고 등급별로 시간을 다르게(0.6/1.0/
/// 1.4초) 그렸는데, 기획서 6.5.5는 1초 간격 순차다. 기획서를 따른다.
///
/// ponytail: 공개 진행도는 서버에서 따로 받지 않는다. 개봉 시각 하나만 복제되고 공개
/// 여부는 서버와 같은 식(`LootSlots.RevealedCount`)으로 클라이언트가 계산한다. 칸이
/// 드러날 때마다 RPC를 보내면 5칸에 RPC 5개다.
public sealed class BoxLootPopup : UIPopup
{
    /// 한 칸의 부품 묶음. 배열 크기는 `LootSlots.MaxTypes`와 같아야 한다.
    [Serializable] public class Cell
    {
        public GameObject Root;
        public Image Frame;
        public Image Icon;
        public TMP_Text Name;
        public TMP_Text Count;
        public TMP_Text Weight;

        /// 가려진 칸에만 보이는 것들. `?`와 남은 시간 안내다.
        public GameObject Blind;
        public TMP_Text BlindIn;
    }

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

    [Header("칸")]
    [SerializeField] Cell[] cells = Array.Empty<Cell>();

    /// 칸 사이의 금색 구분 띠. 칸이 하나도 없으면 띠만 남아 통짜 금색으로 보인다.
    [SerializeField] GameObject slotStrip;

    [Header("바닥")]
    [SerializeField] TMP_Text holdHint;
    [SerializeField] TMP_Text warning;

    [Header("색")]
    [SerializeField] Color blindColor = new(0.07f, 0.04f, 0.12f, 1f);
    [SerializeField] Color readyColor = new(0.04f, 0.06f, 0.09f, 1f);
    [SerializeField] Color emptyColor = new(0.10f, 0.10f, 0.11f, 0.6f);

    [Header("연출")]
    [SerializeField] float flySeconds = 0.45f;
    [SerializeField] float bagPunchScale = 0.35f;
    [SerializeField] float bagPunchSeconds = 0.3f;

    /// 재료 아이콘. 비워 두면 이름 글자로만 그린다 — 아트가 붙기 전에도 규칙은 확인된다.
    [SerializeField] IngredientIcon[] icons = Array.Empty<IngredientIcon>();

    /// 날아가는 사본이 붙을 곳. 칸보다 위에 그려져야 해서 프리팹에 따로 둔다.
    [SerializeField] RectTransform flyLayer;

    ItemBox box;
    PlayerInteract interact;
    RectTransform bagAnchor;

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
        for (var i = 0; i < cells.Length; i++)
        {
            var index = i;
            var frame = cells[i]?.Frame;
            if (frame == null) continue;

            var button = frame.GetComponent<Button>();
            if (button == null) button = frame.gameObject.AddComponent<Button>();
            button.targetGraphic = frame;
            UIButtons.Wire(button, () => OnCellClicked(index));
        }
    }

    /// `MatchFlow`가 열려 있는 상자와 로컬 플레이어를 넘겨 준다. `anchor`는 아이템이 빨려
    /// 들어갈 HUD의 가방 아이콘이며 없으면 연출만 생략된다.
    public void Bind(ItemBox value, PlayerInteract holder, RectTransform anchor)
    {
        box = value;
        interact = holder;
        bagAnchor = anchor;
        bag = holder != null ? holder.GetComponent<PlayerInventory>() : null;
        if (anchor != null) bagScale = anchor.localScale;
        lastRevealed = -1;
        lastSignature = -1;
    }

    public override void OnHide()
    {
        box = null;
        interact = null;
        bagAnchor = null;
        bag = null;
    }

    void Update()
    {
        if (box == null) return;

        var revealed = box.RevealedCount;
        var signature = Signature();
        if (revealed == lastRevealed && signature == lastSignature) return;

        lastRevealed = revealed;
        lastSignature = signature;
        Render(revealed);
    }

    /// 내용물이 바뀌었는지만 알면 된다. 남의 손에 칸이 비면 이 값이 달라진다.
    int Signature()
    {
        var hash = box.SlotCount * 2 + (HasBag ? 1 : 0);
        for (var i = 0; i < box.SlotCount; i++)
            hash = hash * 397 ^ ((int)box.SlotItem(i) * 31 + box.SlotCountAt(i));
        return hash;
    }

    /// 가방을 메고 있는가. 참조가 없으면(연출 생략 상태) 막지 않는다 — 담기 판정은
    /// 어차피 서버가 한다.
    bool HasBag => bag == null || bag.HasBag;

    void Render(int revealed)
    {
        var slots = box.SlotCount;
        var remaining = 0;
        for (var i = 0; i < slots; i++) if (box.SlotCountAt(i) > 0) remaining++;

        var hidden = Mathf.Max(0, slots - revealed);
        Set(tierLabel, $"TIER {box.Tier}");
        Set(title, !HasBag
            ? "가방을 묻어 뒀다 — 담을 수 없다"
            : $"슬롯 {slots} · 남은 전리품 {remaining}");
        Set(revealValue, hidden > 0 ? $"{hidden}칸 공개 중…" : "전부 공개됨");

        // 개인 인벤토리는 없고 무게만 있다 (기획서 6.5.6).
        if (bag != null)
        {
            Set(bagPercent, $"{Mathf.RoundToInt(bag.LoadRatio * 100f)}%");
            Set(bagWeight, $"{bag.Carried:0.0} KG");
        }

        Set(holdHint, "HOLD F — 담기 (개당 0.2초)");
        Set(warning, "이동 · 대시 · 피격 시 창이 닫히고 개봉부터 다시");

        if (slotStrip != null) slotStrip.SetActive(slots > 0);

        for (var i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            if (cell?.Root == null) continue;

            if (i >= slots) { cell.Root.SetActive(false); continue; }
            cell.Root.SetActive(true);

            var item = box.SlotItem(i);
            var count = box.SlotCountAt(i);
            var open = i < revealed;
            var takable = open && count > 0;

            if (cell.Blind != null) cell.Blind.SetActive(!open);
            Set(cell.BlindIn, !open ? "IN 1s" : string.Empty);

            if (cell.Frame != null)
            {
                cell.Frame.color = !open ? blindColor : takable ? readyColor : emptyColor;
                cell.Frame.raycastTarget = takable && HasBag;
            }

            Set(cell.Name, !open ? "— — —" : count > 0 ? DisplayNames.Of(item) : "");
            Set(cell.Count, takable && count > 1 ? $"×{count}" : "");
            Set(cell.Weight, takable ? $"{Ingredients.WeightOf(item) * count:0.0} KG" : "");

            var sprite = takable ? SpriteOf(item) : null;
            if (cell.Icon != null)
            {
                cell.Icon.sprite = sprite;
                cell.Icon.enabled = sprite != null;
            }
        }
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
    void OnCellClicked(int index)
    {
        if (box == null || interact == null || !HasBag) return;
        if (!box.IsSlotRevealed(index) || box.SlotCountAt(index) <= 0) return;

        interact.TakeSlotClient(index);
        FlyToBag(cells[index]);
    }

    /// 누른 칸의 사본을 가방 아이콘으로 날린다. 원본을 옮기면 칸 배치가 무너진다.
    void FlyToBag(Cell cell)
    {
        if (bagAnchor == null || flyLayer == null || cell?.Frame == null) return;

        // 창이 닫히면 필드가 비므로 지역 변수로 잡아 둔다. 트윈은 창보다 오래 산다.
        var anchor = bagAnchor;
        var scale = bagScale;
        var source = (RectTransform)cell.Frame.transform;

        var ghost = new GameObject("Ghost", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)ghost.transform;
        rect.SetParent(flyLayer, false);
        rect.sizeDelta = source.rect.size;
        rect.position = source.position;

        var image = ghost.GetComponent<Image>();
        image.sprite = cell.Icon != null ? cell.Icon.sprite : null;
        image.color = image.sprite != null ? Color.white : cell.Frame.color;
        image.raycastTarget = false;

        DOTween.Sequence()
            .Append(rect.DOMove(anchor.position, flySeconds).SetEase(Ease.InQuad))
            .Join(rect.DOScale(0.2f, flySeconds).SetEase(Ease.InQuad))
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
            .OnComplete(() => Destroy(ghost))
            .SetUpdate(true);       // 팝업이 떠 있는 동안 시간 배율이 어떻든 돈다
    }

    static void Set(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }
}
