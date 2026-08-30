using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 캐릭터 선택 화면 (기획서 9장). 레이아웃은 `UI_목업.pptx` 2번이다.
///
/// 밤 칸의 성격은 아직 갈려 있다 — 기획서 9.1은 밤 패시브 표를 전부 취소선 처리하고
/// "밤은 액티브로 고정"이라고 적었는데, 목업은 `NIGHT PASSIVE`로 그려져 있다. 그래서
/// 밤 소제목을 `Bind`의 인자로 받는다. 어느 쪽으로 확정되든 이 화면은 손댈 것이 없고
/// `CharacterCatalog`의 두 칸과 소제목 문자열만 바뀐다.
///
/// 고르는 것은 이 화면이고 확정하는 것은 서버다. 팀 내 중복 픽 금지(9.1)는 여기서
/// 판정하지 않는다 — 짝꿍이 무엇을 골랐는지는 복제 상태로만 알 수 있고, 두 클라이언트가
/// 각자 판정하면 동시에 같은 것을 고른 순간 결과가 갈린다.
public sealed class UICharacterSelectScreen : UIScreen
{
    const int Columns = 4;
    const float CardWidth = 289f, CardHeight = 457f;
    const float CardStepX = 305f, CardStepY = 474f;
    const float GridX = 54f, GridY = 125f;

    /// 짝꿍이 이미 집어서 고를 수 없는 카드의 바닥색. 목업 2번의 어두운 카드가 이것이다.
    static readonly Color TakenCard = new(8f / 255f, 5f / 255f, 3f / 255f, 1f);

    TMP_Text waitLabel, timer;
    TMP_Text selectedName, dayName, dayEffect, nightCaption, nightName, nightEffect, footnote;
    Button confirmButton, backButton;

    readonly List<Button> cardButtons = new();
    readonly List<UnityEngine.UI.Image> cardBacks = new();

    int selected = -1;
    Action<int> onPick;

    /// 짝꿍이 집어 간 칸. 카드를 다시 칠할 때마다 필요해서 들고 있는다 — 클릭으로
    /// 다시 칠할 때 이것이 없으면 잠긴 카드가 평범한 카드로 되돌아간다.
    IReadOnlyList<bool> taken;

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

        UITheme.Box(transform, "Backdrop", UITheme.Ink, 0f, 0f, 1920f, 1080f);

        UITheme.Caption(transform, "CHARACTER SELECT", 54f, 65f, 289f);
        UITheme.Text(transform, "Rule", "팀 내 중복 픽 금지 · 발동 키 없는 상시 패시브", 10f,
                     UITheme.Cream, 335f, 62f, 310f, 22f);
        waitLabel = UITheme.Text(transform, "WaitLabel", "짝꿍 선택 대기", 10f, UITheme.Cream,
                                 1600f, 50f, 172f, 21f, TextAlignmentOptions.TopRight);
        timer = UITheme.Text(transform, "Timer", "0:00", 33f, UITheme.Red,
                             1760f, 36f, 114f, 48f, TextAlignmentOptions.TopRight);

        UITheme.Rule(transform, UITheme.Gold, 54f, 98f, 1812f);

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

            var card = UITheme.Box(transform, $"Card{i}", UITheme.Panel,
                                   x, y, CardWidth, CardHeight);

            // 카드는 눌린다. `UITheme.Box`는 장식용이라 레이캐스트를 꺼 두므로 여기서 켠다.
            var image = card.GetComponent<UnityEngine.UI.Image>();
            image.raycastTarget = true;
            cardBacks.Add(image);

            var button = card.gameObject.AddComponent<UnityEngine.UI.Button>();
            button.targetGraphic = image;
            cardButtons.Add(button);

            UITheme.Box(card, "Portrait", UITheme.Placeholder, 15f, 15f, 259f, 355f);
            UITheme.Text(card, "Name", all[i].Name, 15f, UITheme.Cream,
                         2f, 380f, 284f, 31f, TextAlignmentOptions.Top);
            UITheme.Text(card, "DayTag", $"낮 {all[i].DayName}", 8f, UITheme.GoldLit,
                         12f, 423f, 131f, 17f, TextAlignmentOptions.Top);
            UITheme.Text(card, "NightTag", $"밤 {all[i].NightName}", 8f, UITheme.Blue,
                         145f, 423f, 131f, 17f, TextAlignmentOptions.Top);
        }
    }

    void BuildDetail()
    {
        var panel = UITheme.Box(transform, "Detail", UITheme.Panel, 1286f, 125f, 580f, 848f);
        UITheme.Box(panel, "Portrait", UITheme.Placeholder, 21f, 21f, 212f, 806f);
        UITheme.Text(panel, "PortraitNote", "캐릭터\n전신 렌더\n420×760", 9f, UITheme.Ink,
                     21f, 396f, 212f, 60f, TextAlignmentOptions.Top);

        UITheme.Text(panel, "SelectedCaption", "SELECTED", 8f, UITheme.Gold,
                     253f, 25f, 332f, 16f);
        selectedName = UITheme.Text(panel, "SelectedName", "—", 30f, UITheme.Cream,
                                    253f, 41f, 332f, 48f);

        UITheme.Rule(panel, UITheme.GoldLit, 253f, 103f, 302f);
        UITheme.Text(panel, "DayCaption", "DAY PASSIVE", 8f, UITheme.GoldLit,
                     253f, 118f, 332f, 16f);
        dayName = UITheme.Text(panel, "DayName", string.Empty, 18f, UITheme.Cream,
                               253f, 136f, 332f, 36f);
        dayEffect = UITheme.Text(panel, "DayEffect", string.Empty, 11f, UITheme.Cream,
                                 253f, 170f, 311f, 52f);

        UITheme.Rule(panel, UITheme.Blue, 253f, 236f, 302f);
        nightCaption = UITheme.Text(panel, "NightCaption", "NIGHT", 8f, UITheme.Blue,
                                    253f, 251f, 332f, 16f);
        nightName = UITheme.Text(panel, "NightName", string.Empty, 18f, UITheme.Cream,
                                 253f, 269f, 332f, 36f);
        nightEffect = UITheme.Text(panel, "NightEffect", string.Empty, 11f, UITheme.Cream,
                                   253f, 303f, 311f, 28f);

        footnote = UITheme.Text(panel, "Footnote", string.Empty, 10f, UITheme.Cream,
                                253f, 779f, 311f, 48f);
    }

    void BuildFooter()
    {
        // 버튼 라벨을 비우고 글자를 따로 놓는다. `ESC` 칩과 `뒤로`가 가로로 나란히
        // 서는 자리라, 라벨 하나를 버튼 폭 가운데에 두면 둘이 겹친다.
        backButton = UITheme.Button(transform, "Back", string.Empty, false, 1286f, 991f, 180f, 63f);
        UITheme.Text(backButton.transform, "EscHint", "ESC", 8f, UITheme.Cream,
                     42f, 26f, 51f, 16f, TextAlignmentOptions.Top);
        UITheme.Text(backButton.transform, "BackLabel", "뒤로", 14f, UITheme.Cream,
                     98f, 19f, 44f, 29f, TextAlignmentOptions.Top);

        confirmButton = UITheme.Button(transform, "Confirm", "선택 완료", true,
                                       1480f, 991f, 386f, 63f);
    }

    /// 화면을 열 때 한 번. `taken`은 `CharacterCatalog.All`과 같은 순서로, 짝꿍이 이미
    /// 집어서 고를 수 없는 칸이다 (기획서 9.1 중복 픽 금지).
    ///
    /// `nightHeading`은 밤 칸의 소제목이다. 기획서 9.1이 확정되기 전까지 부르는 쪽이 정한다.
    public void Bind(IReadOnlyList<bool> locked, int preselect, string nightHeading,
                     string note, Action<int> pick, Action confirm, Action back)
    {
        Build();

        onPick = pick;
        taken = locked;
        nightCaption.text = string.IsNullOrEmpty(nightHeading) ? "NIGHT" : nightHeading;
        footnote.text = note ?? string.Empty;

        UIButtons.Wire(confirmButton, confirm);
        UIButtons.Wire(backButton, back);

        for (var i = 0; i < cardButtons.Count; i++)
        {
            cardButtons[i].interactable = !Blocked(i);

            // 람다가 루프 변수를 잡지 않도록 한 번 복사한다. 잡으면 카드 8장이 전부
            // 마지막 인덱스를 고른다.
            var index = i;
            UIButtons.Wire(cardButtons[i], () => Select(index, true));
        }

        // 열면서 고르는 것은 표시일 뿐이라 서버에 알리지 않는다. 알리면 화면을 여는
        // 것만으로 픽이 확정돼 짝꿍의 선택지를 잠근다.
        Select(preselect >= 0 && preselect < cardButtons.Count ? preselect : 0, false);
    }

    bool Blocked(int i) => taken != null && i < taken.Count && taken[i];

    /// 짝꿍이 아직 고르는 중인지. 목업 우상단의 대기 표시다.
    public void SetWaiting(bool waiting, float seconds)
    {
        if (timer == null) return;
        waitLabel.text = waiting ? "짝꿍 선택 대기" : string.Empty;
        var whole = Mathf.CeilToInt(Mathf.Max(seconds, 0f));
        timer.text = $"{whole / 60}:{whole % 60:00}";
    }

    void Select(int index, bool notify)
    {
        var all = CharacterCatalog.All;
        if (index < 0 || index >= all.Length) return;

        selected = index;
        for (var i = 0; i < cardBacks.Count; i++)
            cardBacks[i].color = Blocked(i) ? TakenCard
                               : i == selected ? UITheme.PanelDeep
                               : UITheme.Panel;

        selectedName.text = all[index].Name;
        dayName.text = all[index].DayName;
        dayEffect.text = all[index].DayEffect;
        nightName.text = all[index].NightName;
        nightEffect.text = all[index].NightEffect;

        if (notify) onPick?.Invoke(index);
    }

    /// 지금 고른 칸. `CharacterCatalog.All`의 인덱스다.
    public int Selected => selected;
}
