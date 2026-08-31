using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 캐릭터 선택 화면 (기획서 9장). 레이아웃은 `UI_목업.pptx` 2번이다.
///
/// 밤 칸의 성격은 아직 갈려 있다 — 기획서 9.1은 밤 패시브 표를 전부 취소선 처리하고
/// "밤은 액티브로 고정"이라고 적었는데, 목업은 여전히 `NIGHT PASSIVE`로 그려져 있다.
/// 그래서 밤 소제목을 `Bind` 인자로 받는다. 어느 쪽으로 확정되든 이 화면은 손댈 것이
/// 없고 `CharacterCatalog`의 두 칸과 소제목 문자열만 바뀐다.
///
/// 고르는 것은 이 화면이고 확정하는 것은 서버다. 팀 내 중복 픽 금지(9.1)는 여기서
/// 판정하지 않는다 — 짝꿍이 무엇을 골랐는지는 복제 상태로만 알 수 있고, 두 클라이언트가
/// 각자 판정하면 동시에 같은 것을 고른 순간 결과가 갈린다.
public sealed class UICharacterSelectScreen : UIScreen
{
    /// 이미 누가 집어 간 카드. 목업 2번의 카드 좌상단 라벨(팀 색 점 + "2팀 · 안개 등대")이다.
    public readonly struct Claim
    {
        public readonly int Character;
        public readonly string Label;
        public readonly Color Color;
        public Claim(int character, string label, Color color)
        {
            Character = character; Label = label; Color = color;
        }
    }

    const int Columns = 4;
    const float CardWidth = 289f, CardHeight = 457f;
    const float CardStepX = 305f, CardStepY = 473f;
    const float GridX = 54f, GridY = 125f;

    /// 선점된 카드를 덮는 색. 목업 2번의 어두운 카드가 이것이다.
    static readonly Color TakenCard = new(8f / 255f, 5f / 255f, 3f / 255f, 1f);

    RectTransform stage;
    TMP_Text waitLabel, timer;
    TMP_Text selectedName, dayName, dayEffect, nightCaption, nightName, nightEffect, footnote;
    TMP_Text nameplateName, nameplateNote;
    RectTransform nameplateSwatch;
    Button confirmButton, backButton, prevButton, nextButton;

    readonly List<Button> cardButtons = new();
    readonly List<UnityEngine.UI.Image> cardBacks = new();

    /// 카드마다 하나씩. 선점되지 않았으면 통째로 꺼 둔다.
    readonly List<GameObject> claimBadges = new();
    readonly List<UnityEngine.UI.Image> claimDots = new();
    readonly List<TMP_Text> claimLabels = new();
    readonly List<RectTransform> claimChips = new();

    readonly List<Button> colorButtons = new();
    readonly List<RectTransform> colorMarks = new();

    int selected = -1;
    int colorIndex;
    Action<int> onPick;
    Action<int> onColor;

    /// 남이 집어 간 칸. 카드를 다시 칠할 때마다 필요해서 들고 있는다 — 클릭으로 다시
    /// 칠할 때 이것이 없으면 잠긴 카드가 평범한 카드로 되돌아간다.
    IReadOnlyList<Claim> claims;

    bool built;

    protected override void Awake()
    {
        base.Awake();
        Build();
    }

    void Build()
    {
        if (built) return;
        built = true;

        stage = UITheme.Stage(transform, UITheme.Ink);

        UITheme.Caption(stage, "CHARACTER SELECT", 54f, 59f, 253f);
        UITheme.Text(stage, "Rule", "팀 내 중복 픽 금지 · 낮 패시브 1 + 밤 액티브 1", 10f,
                     UITheme.Cream, 302f, 62f, 304f, 22f);
        waitLabel = UITheme.Text(stage, "WaitLabel", "짝꿍 선택 대기", 10f, UITheme.Cream,
                                 1590f, 50f, 192f, 20f, TextAlignmentOptions.TopRight);
        timer = UITheme.Text(stage, "Timer", "0:00", 33f, UITheme.Red,
                             1760f, 36f, 114f, 48f, TextAlignmentOptions.TopRight);

        UITheme.Rule(stage, UITheme.Gold, 54f, 98f, 1812f);

        BuildCards();
        BuildDetail();
        BuildFooter();
    }

    void BuildCards()
    {
        var all = CharacterCatalog.All;
        for (var i = 0; i < all.Length; i++)
        {
            var x = GridX + i % Columns * CardStepX;
            var y = GridY + i / Columns * CardStepY;

            var card = UITheme.Box(stage, $"Card{i}", UITheme.Panel, x, y, CardWidth, CardHeight);

            // 카드는 눌린다. `UITheme.Box`는 장식용이라 레이캐스트를 꺼 두므로 여기서 켠다.
            var image = card.GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = true;
            cardBacks.Add(image);

            var button = card.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            cardButtons.Add(button);

            UITheme.Box(card, "Portrait", UITheme.Placeholder, 15f, 15f, 259f, 390f);
            UITheme.Text(card, "Name", all[i].Name, 15f, UITheme.Cream,
                         2f, 417f, 284f, 29f, TextAlignmentOptions.Top);

            BuildClaimBadge(card);
        }
    }

    /// 선점 라벨. 카드를 덮는 판과 좌상단 칩이 한 덩어리다.
    void BuildClaimBadge(RectTransform card)
    {
        var badge = UITheme.Box(card, "Claim", TakenCard, 1f, 1f, CardWidth - 2f, CardHeight - 2f);
        claimBadges.Add(badge.gameObject);

        var chip = UITheme.Box(badge, "Chip", TakenCard, 0f, 0f, 120f, 27f);
        claimChips.Add(chip);

        claimDots.Add(UITheme.Box(chip, "Dot", UITheme.Cream, 10f, 9f, 9f, 9f)
                             .GetComponent<UnityEngine.UI.Image>());
        claimLabels.Add(UITheme.Text(chip, "Label", string.Empty, 9f, UITheme.Cream,
                                     26f, 6f, 200f, 19f));

        badge.gameObject.SetActive(false);
    }

    void BuildDetail()
    {
        var panel = UITheme.Box(stage, "Detail", UITheme.Panel, 1286f, 125f, 580f, 849f);

        UITheme.Box(panel, "Portrait", UITheme.Placeholder, 21f, 21f, 250f, 661f);
        UITheme.Text(panel, "PortraitNote", "캐릭터\n전신 렌더\n420×760", 9f, UITheme.Ink,
                     21f, 325f, 250f, 60f, TextAlignmentOptions.Top);

        // 초상 넘김. 목업 2번에 화살표만 있고 무엇을 넘기는지는 적혀 있지 않아,
        // 카드 그리드의 선택을 좌우로 옮기는 것으로 둔다.
        prevButton = MakeArrow(panel, "Prev", "‹", 21f, 323f);
        nextButton = MakeArrow(panel, "Next", "›", 235f, 323f);

        BuildNameplate(panel);

        const float col = 291f;
        UITheme.Text(panel, "SelectedCaption", "SELECTED", 8f, UITheme.Gold, col, 25f, 290f, 20f);
        selectedName = UITheme.Text(panel, "SelectedName", "—", 30f, UITheme.Cream,
                                    col, 45f, 290f, 48f);

        UITheme.Rule(panel, UITheme.GoldLit, col, 107f, 264f);
        UITheme.Text(panel, "DayCaption", "DAY PASSIVE", 8f, UITheme.GoldLit, col, 122f, 290f, 20f);
        dayName = UITheme.Text(panel, "DayName", string.Empty, 18f, UITheme.Cream,
                               col, 144f, 290f, 34f);
        dayEffect = UITheme.Text(panel, "DayEffect", string.Empty, 11f, UITheme.Cream,
                                 col, 176f, 272f, 52f);

        UITheme.Rule(panel, UITheme.Blue, col, 242f, 264f);
        nightCaption = UITheme.Text(panel, "NightCaption", "NIGHT ACTIVE", 8f, UITheme.Blue,
                                    col, 257f, 290f, 20f);
        nightName = UITheme.Text(panel, "NightName", string.Empty, 18f, UITheme.Cream,
                                 col, 279f, 290f, 34f);
        nightEffect = UITheme.Text(panel, "NightEffect", string.Empty, 11f, UITheme.Cream,
                                   col, 311f, 272f, 52f);

        footnote = UITheme.Text(panel, "Footnote", string.Empty, 10f, UITheme.Cream,
                                col, 780f, 272f, 48f);
    }

    static Button MakeArrow(Transform parent, string name, string glyph, float x, float y)
    {
        var button = UITheme.Button(parent, name, string.Empty, false, x, y, 36f, 58f);
        button.image.color = TakenCard;
        UITheme.Text(button.transform, "Glyph", glyph, 15f, UITheme.Gold,
                     0f, 18f, 36f, 24f, TextAlignmentOptions.Top);
        return button;
    }

    /// 인게임 네임플레이트에 그대로 쓰이는 색과 이름 (목업 2번 `MY NAMEPLATE`).
    void BuildNameplate(Transform panel)
    {
        var block = UITheme.Box(panel, "Nameplate", UITheme.Panel, 21f, 694f, 250f, 134f);
        UITheme.Text(block, "Caption", "MY NAMEPLATE", 8f, UITheme.Gold, 13f, 13f, 246f, 19f);

        nameplateSwatch = UITheme.Box(block, "Swatch", UITheme.TeamColors[0], 13f, 41f, 12f, 12f);
        nameplateName = UITheme.Text(block, "Name", "—", 13f, UITheme.Cream, 33f, 36f, 200f, 25f);
        nameplateNote = UITheme.Text(block, "Note",
                                     "인게임에서 캐릭터 위에 이 색과 이름으로 표시된다",
                                     8f, UITheme.Cream, 13f, 63f, 232f, 32f);

        for (var i = 0; i < UITheme.TeamColors.Length; i++)
        {
            var swatch = UITheme.Box(block, $"Color{i}", UITheme.TeamColors[i],
                                     13f + i * 38.4f, 101f, 32f, 20f);
            var image = swatch.GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = true;

            var button = swatch.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            colorButtons.Add(button);

            // 고른 색 아래 밑줄. 색 자체를 바꾸면 무슨 색인지 알 수 없게 된다.
            var mark = UITheme.Box(swatch, "Mark", UITheme.Cream, 0f, 22f, 32f, 2f);
            mark.gameObject.SetActive(false);
            colorMarks.Add(mark);
        }
    }

    void BuildFooter()
    {
        // 버튼 라벨을 비우고 글자를 따로 놓는다. `ESC` 칩과 `뒤로`가 가로로 나란히 서는
        // 자리라, 라벨 하나를 버튼 폭 가운데에 두면 둘이 겹친다.
        backButton = UITheme.Button(stage, "Back", string.Empty, false, 1286f, 992f, 180f, 62f);
        var chip = UITheme.Box(backButton.transform, "EscChip", UITheme.PanelDeep,
                               44f, 17f, 45f, 28f);
        UITheme.Text(chip, "Esc", "ESC", 8f, UITheme.Cream, 0f, 6f, 45f, 20f,
                     TextAlignmentOptions.Top);
        UITheme.Text(backButton.transform, "BackLabel", "뒤로", 14f, UITheme.Cream,
                     95f, 20f, 45f, 27f, TextAlignmentOptions.Top);

        confirmButton = UITheme.Button(stage, "Confirm", "선택 완료", true, 1480f, 992f, 386f, 62f);
    }

    /// 화면을 열 때 한 번. `taken`은 남이 이미 집어 간 카드들이고, 목업 2번처럼 카드
    /// 좌상단에 팀 색과 이름표가 붙는다 (기획서 9.1 중복 픽 금지).
    ///
    /// `nightHeading`은 밤 칸의 소제목이다. 기획서 9.1이 확정되기 전까지 부르는 쪽이 정한다.
    public void Bind(IReadOnlyList<Claim> taken, int preselect, int myColor, string myName,
                     string nightHeading, string note,
                     Action<int> pick, Action<int> pickColor, Action confirm, Action back)
    {
        Build();

        onPick = pick;
        onColor = pickColor;
        claims = taken;

        nightCaption.text = string.IsNullOrEmpty(nightHeading) ? "NIGHT" : nightHeading;
        footnote.text = note ?? string.Empty;
        nameplateName.text = myName ?? "—";

        UIButtons.Wire(confirmButton, confirm);
        UIButtons.Wire(backButton, back);
        UIButtons.Wire(prevButton, () => Step(-1));
        UIButtons.Wire(nextButton, () => Step(1));

        for (var i = 0; i < cardButtons.Count; i++)
        {
            cardButtons[i].interactable = ClaimOf(i) < 0;

            // 람다가 루프 변수를 잡지 않도록 한 번 복사한다. 잡으면 카드 8장이 전부
            // 마지막 인덱스를 고른다.
            var index = i;
            UIButtons.Wire(cardButtons[i], () => Select(index, true));
        }

        for (var i = 0; i < colorButtons.Count; i++)
        {
            var index = i;
            UIButtons.Wire(colorButtons[i], () => SelectColor(index, true));
        }

        SelectColor(Mathf.Clamp(myColor, 0, colorButtons.Count - 1), false);
        RefreshClaims();

        // 열면서 고르는 것은 표시일 뿐이라 서버에 알리지 않는다. 알리면 화면을 여는
        // 것만으로 픽이 확정돼 짝꿍의 선택지를 잠근다.
        Select(preselect >= 0 && preselect < cardButtons.Count ? preselect : 0, false);
    }

    /// 짝꿍이 픽을 확정했을 때 부른다. 잠금만 갈아 끼우고 내가 고르던 칸과 색은 그대로 둔다 —
    /// `Bind`를 다시 부르면 둘 다 인자 기본값으로 되돌아간다.
    ///
    /// 화면을 열어 둔 동안 짝꿍이 무엇을 집었는지는 복제 상태로만 알 수 있으므로, 이
    /// 경로가 없으면 이미 남이 가져간 칸을 계속 고를 수 있게 그려 준다 (기획서 9.1).
    public void SetClaims(IReadOnlyList<Claim> taken)
    {
        if (!built) return;

        claims = taken;
        for (var i = 0; i < cardButtons.Count; i++)
            cardButtons[i].interactable = ClaimOf(i) < 0;
        RefreshClaims();

        // 내가 보고 있던 칸을 남이 먼저 가져갔으면 빈 칸으로 옮긴다. 서버에 알리는 것은
        // 실제로 픽이 바뀐 것이라 맞다.
        if (ClaimOf(selected) >= 0) Step(1);
    }

    /// 짝꿍이 아직 고르는 중인지. 목업 우상단의 대기 표시다.
    public void SetWaiting(bool waiting, float seconds)
    {
        if (timer == null) return;
        waitLabel.text = waiting ? "짝꿍 선택 대기" : string.Empty;
        var whole = Mathf.CeilToInt(Mathf.Max(seconds, 0f));
        timer.text = $"{whole / 60}:{whole % 60:00}";
    }

    /// 지금 고른 칸. `CharacterCatalog.All`의 인덱스다.
    public int Selected => selected;

    /// 지금 고른 팀 색. `UITheme.TeamColors`의 인덱스다.
    public int ColorIndex => colorIndex;

    int ClaimOf(int character)
    {
        if (claims == null) return -1;
        for (var i = 0; i < claims.Count; i++)
            if (claims[i].Character == character) return i;
        return -1;
    }

    void RefreshClaims()
    {
        for (var i = 0; i < claimBadges.Count; i++)
        {
            var at = ClaimOf(i);
            claimBadges[i].SetActive(at >= 0);
            if (at < 0) continue;

            claimDots[i].color = claims[at].Color;
            claimLabels[i].text = claims[at].Label ?? string.Empty;
            // 칩은 글자 길이에 맞춰 줄인다. 고정 폭이면 짧은 이름에서 빈 칸이 남는다.
            claimChips[i].sizeDelta =
                new Vector2(claimLabels[i].preferredWidth + 38f, claimChips[i].sizeDelta.y);
        }
    }

    /// 화살표로 선택을 옮긴다. 선점된 칸은 건너뛴다.
    void Step(int delta)
    {
        var count = cardButtons.Count;
        if (count == 0) return;

        for (var n = 1; n <= count; n++)
        {
            var next = ((selected + delta * n) % count + count) % count;
            if (ClaimOf(next) >= 0) continue;
            Select(next, true);
            return;
        }
    }

    void Select(int index, bool notify)
    {
        var all = CharacterCatalog.All;
        if (index < 0 || index >= all.Length) return;

        selected = index;
        for (var i = 0; i < cardBacks.Count; i++)
            cardBacks[i].color = i == selected ? UITheme.PanelDeep : UITheme.Panel;

        selectedName.text = all[index].Name;
        dayName.text = all[index].DayName;
        dayEffect.text = all[index].DayEffect;
        nightName.text = all[index].NightName;
        nightEffect.text = all[index].NightEffect;

        if (notify) onPick?.Invoke(index);
    }

    void SelectColor(int index, bool notify)
    {
        if (index < 0 || index >= colorMarks.Count) return;

        colorIndex = index;
        for (var i = 0; i < colorMarks.Count; i++)
            colorMarks[i].gameObject.SetActive(i == index);

        nameplateSwatch.GetComponent<UnityEngine.UI.Image>().color = UITheme.TeamColors[index];

        if (notify) onColor?.Invoke(index);
    }
}
