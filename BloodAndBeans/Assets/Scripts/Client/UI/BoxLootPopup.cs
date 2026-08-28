using System;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 상자 루팅 창 (기획서 7.2-2). 개봉 게이지가 다 차면 열리고, 이동하거나 맞아서
/// 서버가 세션을 닫을 때까지 떠 있는다.
///
/// 칸은 종류 기준 5개다. 열린 직후에는 전부 `?`로 가려져 있고 1초 간격으로 하나씩
/// 정체를 드러낸다. 드러난 칸을 누르면 통째로 가방에 들어가고, 아이콘이 HUD의 가방으로
/// 날아가 빨려 들어간다.
///
/// 칸은 런타임에 만든다. 프리팹에 다섯 칸을 구워 두면 칸 규칙이 바뀔 때마다 프리팹
/// YAML을 손대야 하고, 이 저장소는 직렬화 참조가 깨지는 그 변경을 금지한다 (AGENTS.md).
///
/// ponytail: 공개 진행도는 서버에서 따로 받지 않는다. 개봉 시각 하나만 복제되고 공개
/// 여부는 서버와 같은 식(`LootSlots.RevealedCount`)으로 클라이언트가 계산한다. 칸이
/// 드러날 때마다 RPC를 보내면 5칸에 RPC 5개다.
public sealed class BoxLootPopup : UIPopup
{
    /// 상자 종류와 남은 칸 수를 쓰는 머리글.
    [SerializeField] TMP_Text label;

    [Header("칸")]
    [SerializeField] Vector2 cellSize = new(96f, 96f);
    [SerializeField] Vector2 cellSpacing = new(12f, 12f);

    /// 머리글이 차지하는 띠의 높이. 프리팹의 머리글 Text는 창 전체를 채우고 가운데
    /// 정렬이라, 자리를 정해 주지 않으면 칸과 같은 곳을 놓고 다퉈 글자가 칸 뒤에 깔린다.
    /// 프리팹 YAML을 고치지 않고 여기서 위쪽 띠로 밀어 올린다.
    [SerializeField] float headerHeight = 34f;

    /// 머리글 띠와 칸 사이의 간격.
    [SerializeField] float headerGap = 6f;

    [Header("색")]
    [SerializeField] Color blindColor = new(0.16f, 0.17f, 0.20f, 0.95f);
    [SerializeField] Color readyColor = new(0.27f, 0.30f, 0.36f, 0.98f);
    [SerializeField] Color emptyColor = new(0.10f, 0.10f, 0.11f, 0.6f);

    [Header("연출")]
    [SerializeField] float flySeconds = 0.45f;
    [SerializeField] float bagPunchScale = 0.35f;
    [SerializeField] float bagPunchSeconds = 0.3f;

    /// 재료 아이콘. 비워 두면 글자로만 그린다 — 아트가 붙기 전에도 규칙은 확인할 수 있다.
    [SerializeField] IngredientIcon[] icons = Array.Empty<IngredientIcon>();

    /// 칸 글자에 쓸 폰트 애셋. 비워 두면 TMP 프로젝트 기본값을 쓴다.
    /// 기본 폰트 애셋에는 한글 글리프가 없으므로, 한글 재료명을 쓰려면 여기에 한글
    /// 폰트 애셋을 이어야 한다 (AGENTS.md 「UI 텍스트는 TextMeshPro를 쓴다」).
    [SerializeField] TMP_FontAsset font;

    [Serializable]
    struct IngredientIcon
    {
        public Ingredient Item;
        public Sprite Sprite;
    }

    /// 한 칸의 구성 요소. 매 갱신마다 GetComponent를 부르지 않으려고 만들 때 묶어 둔다.
    sealed class Cell
    {
        public RectTransform Root;
        public Image Frame;
        public Image Icon;
        public TMP_Text Body;
        public TMP_Text Count;
    }

    readonly Cell[] cells = new Cell[LootSlots.MaxTypes];

    ItemBox box;
    PlayerInteract interact;
    RectTransform bagAnchor;
    RectTransform flyLayer;

    /// 마지막으로 그린 상태. 이것과 같으면 다시 그리지 않는다.
    int lastRevealed = -1;
    int lastSignature = -1;

    protected override void Awake()
    {
        base.Awake();
        BuildCells();
    }

    /// `MatchFlow`가 열려 있는 상자와 로컬 플레이어를 넘겨 준다. `bag`은 아이템이 빨려
    /// 들어갈 HUD의 가방 아이콘이며 없으면 연출만 생략된다.
    public void Bind(ItemBox value, PlayerInteract holder, RectTransform bag)
    {
        box = value;
        interact = holder;
        bagAnchor = bag;
        lastRevealed = -1;
        lastSignature = -1;
    }

    public override void OnHide()
    {
        box = null;
        interact = null;
        bagAnchor = null;
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
        var hash = box.SlotCount;
        for (var i = 0; i < box.SlotCount; i++)
            hash = hash * 397 ^ ((int)box.SlotItem(i) * 31 + box.SlotCountAt(i));
        return hash;
    }

    void Render(int revealed)
    {
        var slots = box.SlotCount;
        var remaining = 0;
        for (var i = 0; i < slots; i++) if (box.SlotCountAt(i) > 0) remaining++;

        if (label != null)
            label.text = $"전리품 ({remaining}/{slots})" +
                (revealed < slots ? "   ·   공개 중…" : "");

        for (var i = 0; i < cells.Length; i++)
        {
            var cell = cells[i];
            if (i >= slots)
            {
                cell.Root.gameObject.SetActive(false);
                continue;
            }

            cell.Root.gameObject.SetActive(true);

            var item = box.SlotItem(i);
            var count = box.SlotCountAt(i);
            var open = i < revealed;
            var takable = open && count > 0;

            cell.Frame.color = !open ? blindColor : takable ? readyColor : emptyColor;
            cell.Body.text = !open ? "?" : count > 0 ? item.ToString() : "";
            cell.Count.text = takable && count > 1 ? $"x{count}" : "";

            var sprite = takable ? SpriteOf(item) : null;
            cell.Icon.sprite = sprite;
            cell.Icon.enabled = sprite != null;
            cell.Body.enabled = sprite == null;

            cell.Frame.raycastTarget = takable;
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
        if (box == null || interact == null) return;
        if (!box.IsSlotRevealed(index) || box.SlotCountAt(index) <= 0) return;

        interact.TakeSlotClient(index);
        FlyToBag(cells[index]);
    }

    /// 누른 칸의 사본을 가방 아이콘으로 날린다. 원본을 옮기면 칸 배치가 무너진다.
    void FlyToBag(Cell cell)
    {
        if (bagAnchor == null || flyLayer == null) return;

        var ghost = new GameObject("Ghost", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)ghost.transform;
        rect.SetParent(flyLayer, false);
        rect.sizeDelta = cell.Root.rect.size;
        rect.position = cell.Root.position;

        var image = ghost.GetComponent<Image>();
        image.sprite = cell.Icon.sprite;
        image.color = cell.Icon.sprite != null ? Color.white : cell.Frame.color;
        image.raycastTarget = false;

        DOTween.Sequence()
            .Append(rect.DOMove(bagAnchor.position, flySeconds).SetEase(Ease.InQuad))
            .Join(rect.DOScale(0.2f, flySeconds).SetEase(Ease.InQuad))
            // 펀치는 시퀀스 밖에서 새로 나는 트윈이라 시퀀스의 SetUpdate를 물려받지 않는다.
            // 직접 걸지 않으면 시간 배율이 0일 때 아이템만 날아가고 가방은 반응하지 않는다.
            .AppendCallback(() => bagAnchor
                .DOPunchScale(Vector3.one * bagPunchScale, bagPunchSeconds, 1, 0.5f)
                .SetUpdate(true))
            .OnComplete(() => Destroy(ghost))
            .SetUpdate(true);       // 팝업이 떠 있는 동안 시간 배율이 어떻든 돈다
    }

    // --- 조립 ---

    void BuildCells()
    {
        var panel = label != null ? (RectTransform)label.transform.parent : (RectTransform)transform;

        // 머리글을 위쪽 띠로 올린다. 창 전체를 채우는 Text를 그대로 두면 칸이 글자 위에
        // 겹쳐 그려진다 (프리팹의 머리글은 가운데 정렬이라 정확히 칸 자리에 앉는다).
        if (label != null)
        {
            var head = label.rectTransform;
            head.anchorMin = new Vector2(0f, 1f);
            head.anchorMax = new Vector2(1f, 1f);
            head.pivot = new Vector2(0.5f, 1f);
            head.offsetMin = new Vector2(0f, -headerHeight);
            head.offsetMax = Vector2.zero;
            head.sizeDelta = new Vector2(0f, headerHeight);
            label.alignment = TextAlignmentOptions.Center;
        }

        var grid = new GameObject("Slots", typeof(RectTransform), typeof(GridLayoutGroup));
        var gridRect = (RectTransform)grid.transform;
        gridRect.SetParent(panel, false);

        // 머리글 띠를 뺀 나머지 공간의 한가운데. 창 높이가 바뀌어도 따라온다.
        gridRect.anchorMin = new Vector2(0f, 0f);
        gridRect.anchorMax = new Vector2(1f, 1f);
        gridRect.pivot = new Vector2(0.5f, 0.5f);
        gridRect.offsetMin = Vector2.zero;
        gridRect.offsetMax = new Vector2(0f, -(headerHeight + headerGap));

        var layout = grid.GetComponent<GridLayoutGroup>();
        layout.cellSize = cellSize;
        layout.spacing = cellSpacing;
        layout.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        layout.constraintCount = LootSlots.MaxTypes;

        // 칸 수는 상자마다 다르다 (3~5). UpperLeft로 두면 4칸짜리 상자가 왼쪽으로 몰려
        // 창 안에서 한쪽으로 치우친 채 그려진다.
        layout.childAlignment = TextAnchor.MiddleCenter;

        for (var i = 0; i < cells.Length; i++) cells[i] = MakeCell(gridRect, i);

        // 날아가는 사본은 칸 위에 그려야 한다. 그리드 안에 두면 레이아웃이 자리를 뺏는다.
        var fly = new GameObject("FlyLayer", typeof(RectTransform));
        flyLayer = (RectTransform)fly.transform;
        flyLayer.SetParent(transform, false);
        flyLayer.anchorMin = Vector2.zero;
        flyLayer.anchorMax = Vector2.one;
        flyLayer.offsetMin = flyLayer.offsetMax = Vector2.zero;
        flyLayer.SetAsLastSibling();
    }

    Cell MakeCell(Transform parent, int index)
    {
        var root = new GameObject($"Slot{index}", typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(parent, false);

        var frame = root.GetComponent<Image>();
        frame.color = blindColor;

        var button = root.GetComponent<Button>();
        // 런타임 AddComponent는 Reset을 부르지 않아 targetGraphic이 비어 있다. 직접 채운다.
        button.targetGraphic = frame;
        button.onClick.AddListener(() => OnCellClicked(index));

        var icon = MakeImage(root.transform, "Icon", new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f));
        icon.enabled = false;
        icon.raycastTarget = false;
        icon.preserveAspect = true;

        var body = MakeText(root.transform, "Body", new Vector2(0f, 0f), new Vector2(1f, 1f));
        body.alignment = TextAlignmentOptions.Center;
        body.fontSize = 18f;

        var count = MakeText(root.transform, "Count", new Vector2(0.45f, 0f), new Vector2(1f, 0.32f));
        count.alignment = TextAlignmentOptions.BottomRight;
        count.fontSize = 16f;

        return new Cell
        {
            Root = (RectTransform)root.transform,
            Frame = frame,
            Icon = icon,
            Body = body,
            Count = count,
        };
    }

    static Image MakeImage(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        Stretch(rect, min, max);
        return go.GetComponent<Image>();
    }

    TMP_Text MakeText(Transform parent, string name, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        Stretch(rect, min, max);

        var text = go.GetComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;      // 비우면 TMP 프로젝트 기본값
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }
}
