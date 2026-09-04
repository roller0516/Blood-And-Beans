using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 캐릭터 선택 화면 (기획서 9장). 레이아웃은 `UI_목업.pptx` 2번이다.
///
/// **머리·상세·네임플레이트 트리는 프리팹에 있다.** 이 클래스는 그것들을 만들지 않고
/// 이어 둔 참조에 값만 넣는다. 다만 **카드는 `CharacterCatalog.All`을 보고 찍어 낸다** —
/// 캐릭터 종 수가 14장 #10 미결이라 프리팹에 몇 장을 깔아 두든 데이터와 어긋난다.
/// 격자 배치는 `cardRoot`의 `GridLayoutGroup`이 맡는다.
///
/// 낮은 발동 키 없는 상시 패시브이고 밤은 쿨타임이 긴 액티브다 (9.1 · 9.2). 목업 2번은
/// 아직 `NIGHT PASSIVE`로 그려져 있어 소제목을 `Bind` 인자로 받는다.
///
/// 고르는 것은 이 화면이고 확정하는 것은 서버다. 팀 내 중복 픽 금지(9.1)는 여기서
/// 판정하지 않는다 — 짝꿍이 무엇을 골랐는지는 복제 상태로만 알 수 있고, 두 클라이언트가
/// 각자 판정하면 동시에 같은 것을 고른 순간 결과가 갈린다.
///
/// 기획서 13장이 캐릭터 패시브를 첫 검증 빌드에서 제외했으므로 지금은 어느 흐름에서도
/// 열지 않는다. 화면과 프리팹만 준비돼 있다.
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

    [Header("머리")]
    [SerializeField] TMP_Text waitLabel;
    [SerializeField] TMP_Text timer;

    [Header("카드")]
    [SerializeField] UICharacterCard cardPrefab;
    [SerializeField] RectTransform cardRoot;

    [Header("상세")]
    [SerializeField] TMP_Text selectedName;
    [SerializeField] TMP_Text dayName;
    [SerializeField] TMP_Text dayEffect;
    [SerializeField] TMP_Text nightCaption;
    [SerializeField] TMP_Text nightName;
    [SerializeField] TMP_Text nightEffect;
    [SerializeField] TMP_Text footnote;
    [SerializeField] Button prevButton;
    [SerializeField] Button nextButton;

    [Header("네임플레이트")]
    [SerializeField] Image nameplateSwatch;
    [SerializeField] TMP_Text nameplateName;
    [SerializeField] Button[] colorButtons = Array.Empty<Button>();
    [SerializeField] GameObject[] colorMarks = Array.Empty<GameObject>();

    [Header("바닥")]
    [SerializeField] Button confirmButton;
    [SerializeField] Button backButton;

    /// 찍어 낸 카드. 인덱스는 `CharacterCatalog.All`과 같다.
    readonly List<UICharacterCard> cards = new();

    int selected = -1;
    int colorIndex;
    Action<int> onPick;
    Action<int> onColor;

    /// 남이 집어 간 칸. 카드를 다시 칠할 때마다 필요해서 들고 있는다.
    IReadOnlyList<Claim> claims;

    /// 화면을 열 때 한 번. `taken`은 남이 이미 집어 간 카드들이고, 목업 2번처럼 카드
    /// 좌상단에 팀 색과 이름표가 붙는다 (기획서 9.1 중복 픽 금지).
    ///
    /// `nightHeading`은 밤 칸의 소제목이다. 목업과 기획서가 갈려 있어 부르는 쪽이 정한다.
    public void Bind(IReadOnlyList<Claim> taken, int preselect, int myColor, string myName,
                     string nightHeading, string note,
                     Action<int> pick, Action<int> pickColor, Action confirm, Action back)
    {
        onPick = pick;
        onColor = pickColor;
        claims = taken;

        if (nightCaption != null)
            nightCaption.text = string.IsNullOrEmpty(nightHeading) ? "NIGHT" : nightHeading;
        if (footnote != null) footnote.text = note ?? string.Empty;
        if (nameplateName != null) nameplateName.text = myName ?? "—";

        UIButtons.Wire(confirmButton, confirm);
        UIButtons.Wire(backButton, back);
        UIButtons.Wire(prevButton, () => Step(-1));
        UIButtons.Wire(nextButton, () => Step(1));

        BuildCards();

        for (var i = 0; i < colorButtons.Length; i++)
        {
            var index = i;
            UIButtons.Wire(colorButtons[i], () => SelectColor(index, true));
        }

        SelectColor(Mathf.Clamp(myColor, 0, Mathf.Max(colorButtons.Length - 1, 0)), false);
        RefreshClaims();

        // 열면서 고르는 것은 표시일 뿐이라 서버에 알리지 않는다. 알리면 화면을 여는
        // 것만으로 픽이 확정돼 짝꿍의 선택지를 잠근다.
        Select(preselect >= 0 && preselect < cards.Count ? preselect : 0, false);
    }

    /// 카탈로그 수만큼 카드를 찍는다. 화면은 재사용되므로 한 번만 만든다.
    ///
    /// `Awake`가 아니라 여기서 만드는 이유는 화면 루트의 <see cref="UIFontScale"/>가 자기
    /// Awake에서 자식 글자를 모으기 때문이다. 그 전에 카드가 있으면 카드가 스스로 먹인
    /// 배율 위에 화면 배율이 한 번 더 곱해진다.
    void BuildCards()
    {
        if (cards.Count > 0) return;
        if (cardPrefab == null || cardRoot == null)
        {
            CDebug.LogError($"[{nameof(UICharacterSelectScreen)}] 카드 프리팹 또는 부모가 비어 있다.");
            return;
        }

        var all = CharacterCatalog.All;
        for (var i = 0; i < all.Length; i++)
        {
            var index = i;
            var card = Instantiate(cardPrefab, cardRoot);
            card.name = $"Card{i}";
            card.Bind(all[i].Name, () => Select(index, true));
            cards.Add(card);
        }
    }

    /// 짝꿍이 픽을 확정했을 때 부른다. 잠금만 갈아 끼우고 고르던 칸과 색은 그대로 둔다 —
    /// `Bind`를 다시 부르면 둘 다 인자 기본값으로 되돌아간다.
    public void SetClaims(IReadOnlyList<Claim> taken)
    {
        claims = taken;
        RefreshClaims();

        // 내가 보고 있던 칸을 남이 먼저 가져갔으면 빈 칸으로 옮긴다.
        if (ClaimOf(selected) >= 0) Step(1);
    }

    /// 짝꿍이 아직 고르는 중인지. 목업 우상단의 대기 표시다.
    public void SetWaiting(bool waiting, float seconds)
    {
        if (waitLabel != null) waitLabel.text = waiting ? "짝꿍 선택 대기" : string.Empty;
        if (timer == null) return;
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
        for (var i = 0; i < cards.Count; i++)
        {
            var at = ClaimOf(i);
            if (at < 0) cards[i].SetClaim(null, default);
            else cards[i].SetClaim(claims[at].Label ?? string.Empty, claims[at].Color);
        }
    }

    /// 화살표로 선택을 옮긴다. 선점된 칸은 건너뛴다.
    void Step(int delta)
    {
        var count = cards.Count;
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
        if (index < 0 || index >= all.Length || index >= cards.Count) return;

        selected = index;
        RefreshSelection();

        Set(selectedName, all[index].Name);
        Set(dayName, all[index].DayName);
        Set(dayEffect, all[index].DayEffect);
        Set(nightName, all[index].NightName);
        Set(nightEffect, all[index].NightEffect);

        if (notify) onPick?.Invoke(index);
    }

    /// 고른 칸을 팀 색으로 칠한다. 칸을 옮길 때도, 팀 색을 바꿀 때도 여기로 온다.
    void RefreshSelection()
    {
        var team = SelectedTeamColor;
        for (var i = 0; i < cards.Count; i++) cards[i].SetSelected(i == selected, team);
    }

    Color SelectedTeamColor
    {
        get
        {
            var palette = UITheme.TeamColors;
            return colorIndex >= 0 && colorIndex < palette.Length
                ? palette[colorIndex] : UITheme.PanelDeep;
        }
    }

    void SelectColor(int index, bool notify)
    {
        if (index < 0 || index >= colorMarks.Length) return;

        colorIndex = index;
        for (var i = 0; i < colorMarks.Length; i++)
            if (colorMarks[i] != null) colorMarks[i].SetActive(i == index);

        if (nameplateSwatch != null && index < UITheme.TeamColors.Length)
            nameplateSwatch.color = UITheme.TeamColors[index];

        // 팀 색이 바뀌면 고른 카드도 따라 바뀐다.
        RefreshSelection();

        if (notify) onColor?.Invoke(index);
    }

    static void Set(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }
}
