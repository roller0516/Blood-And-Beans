using System.Text;
using UnityEngine;
using UnityEngine.UI;

/// 박스를 잡고 있는 동안 뜨는 루팅 창 (기획서 7.2-2). 가려진 칸과 공개된 칸을 함께 보여
/// 주고, 지금 채워지는 홀드 진행도를 막대로 그린다.
///
/// 칸마다 Image를 두지 않고 매치 HUD처럼 글자 한 덩어리만 그린다. 칸 수가 티어마다
/// 달라서(3~5) 프리팹에 고정할 수 없기 때문이다.
///
/// ponytail: 진행도는 서버에서 받지 않고 여기서 다시 잰다. 권위 있는 값은 `ItemBox`의
/// 서버 타이머에 있고 이 막대는 표시 전용이다 — 잡고 있는 본인에게 자기 경과 시간을
/// 보여 주려고 복제를 늘리지 않는다. 서버 시계와 눈에 띄게 어긋나면 그때 복제한다.
public sealed class BoxLootPopup : UIPopup
{
    [SerializeField] Text label;

    [Header("표시")]
    [SerializeField] int barCells = 12;
    [SerializeField] string filledCell = "█";
    [SerializeField] string emptyCell = "░";

    /// 고른 칸을 칠하는 색. 라벨은 rich text가 켜져 있다.
    [SerializeField] Color selectedColor = new(1f, 0.82f, 0.29f);

    readonly StringBuilder text = new();

    /// `selectedColor`의 rich text 표기. 매 프레임 색을 문자열로 바꾸지 않는다.
    string selectedTag;

    ItemBox box;
    PlayerInteract boxHold;
    int team = -1;

    /// 이번 홀드가 시작된 시각. 개봉이 끝나거나 한 칸을 담을 때마다 다시 잡는다.
    float holdStart;

    bool lastOpened;
    int lastRemaining;

    /// 매 프레임 문자열을 새로 만들지 않기 위한 것이다. 막대 칸 수와 0.1초 자리가
    /// 그대로면 그려질 글자도 그대로다.
    int lastFilled = -1;
    int lastTenths = -1;
    int lastSelected = -2;

    protected override void Awake()
    {
        base.Awake();
        selectedTag = "<color=#" + ColorUtility.ToHtmlStringRGB(selectedColor) + ">";
    }

    /// `MatchFlow`가 홀드를 시작한 박스와 그것을 잡고 있는 플레이어를 넘겨 준다.
    public void Bind(ItemBox value, PlayerInteract holder)
    {
        box = value;
        boxHold = holder;
        team = PlayerTeam.Local();
        lastOpened = value != null && value.Opened;
        lastRemaining = value != null ? value.RemainingCount : 0;
        holdStart = Time.time;
        lastFilled = -1;
        lastTenths = -1;
        lastSelected = -2;
    }

    public override void OnHide()
    {
        box = null;
        boxHold = null;
    }

    void Update()
    {
        if (box == null || label == null) return;

        // 개봉이 끝났거나 한 칸을 담았으면 다음 것을 위해 막대를 다시 채운다. 서버가
        // 결정한 결과(`Opened`, 남은 칸)를 보고 따라가는 것이지 여기서 판단하지 않는다.
        var opened = box.Opened;
        var remaining = box.RemainingCount;
        if (opened != lastOpened || remaining != lastRemaining)
        {
            lastOpened = opened;
            lastRemaining = remaining;
            holdStart = Time.time;
        }

        // 서버가 실제로 담을 칸. 고른 칸이 남의 손에 사라지면 서버가 되돌리므로
        // 커서도 같은 규칙으로 따라가야 화면과 결과가 어긋나지 않는다.
        var selected = box.EffectiveSlot(boxHold != null ? boxHold.SelectedSlot : 0);
        var stalled = opened && remaining > 0 && selected < 0;

        var required = Mathf.Max(box.RequiredSecondsFor(team), 0.01f);
        var elapsed = Mathf.Clamp(Time.time - holdStart, 0f, required);
        var empty = opened && remaining == 0;

        var filled = empty ? barCells : Mathf.RoundToInt(elapsed / required * barCells);
        var tenths = empty ? 0 : Mathf.RoundToInt((required - elapsed) * 10f);
        if (filled == lastFilled && tenths == lastTenths && selected == lastSelected) return;

        lastFilled = filled;
        lastTenths = tenths;
        lastSelected = selected;
        label.text = Build(opened, remaining, empty, stalled, selected, filled, tenths);
    }

    string Build(bool opened, int remaining, bool empty, bool stalled, int selected,
                 int filled, int tenths)
    {
        text.Clear();
        text.Append("Tier ").Append(box.Tier);
        if (opened) text.Append(" · ").Append(remaining).Append('/').Append(box.SlotCount);
        else text.Append(" · 길게 눌러 열기");
        text.AppendLine();

        if (opened)
        {
            for (var i = 0; i < box.SlotCount; i++) text.Append(Slot(i, selected)).Append(' ');
            text.AppendLine();
        }

        if (empty)
        {
            text.Append("비었다");
            return text.ToString();
        }

        // 남은 칸이 아직 하나도 안 드러났으면 담기가 진행되지 않는다. 막대만 계속 차면
        // 왜 아무것도 안 들어오는지 알 수 없다.
        if (stalled)
        {
            text.Append("공개 대기");
            return text.ToString();
        }

        for (var i = 0; i < barCells; i++) text.Append(i < filled ? filledCell : emptyCell);
        text.Append("  ").Append(tenths / 10).Append('.').Append(tenths % 10).Append('s');
        if (opened) text.Append("   [1/2] 칸 고르기");
        return text.ToString();
    }

    /// 아직 공개되지 않은 칸은 내용을 숨긴다 (기획서 7.2-2). 무엇이 가려졌는지는
    /// `ItemBox`가 정하고 여기서는 그대로 그린다.
    string Slot(int index, int selected)
    {
        if (!box.IsSlotVisible(index)) return "[??]";

        var content = box.SlotContent(index);
        var body = content == Ingredient.None ? "[  ]" : $"[{content}]";
        return index == selected ? selectedTag + body + "</color>" : body;
    }
}
