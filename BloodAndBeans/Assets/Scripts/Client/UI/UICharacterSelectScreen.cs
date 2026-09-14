using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// 캐릭터 선택 화면 (기획서 9장). 레이아웃은 `BloodAndBeans_CharacterSelect_v0.5.html`의
/// 1안(현행 1열 8인)이다 — 정보 패널이 왼쪽, 캐릭터 8명이 한 줄, 카드 줄이 하단이다.
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
/// 방에 들어가면 곧바로 이 화면이다 (`TitlePresenter.EnterRoom`). 준비 표시와 시작·준비
/// 버튼도 여기 있고(`SetLobby`), 남의 픽은 스팀 로비 멤버 데이터를 타고 들어온다
/// (`SetClaims`).
public sealed class UICharacterSelectScreen : UIScreen
{
    /// 이미 누가 집어 간 카드. 목업 2번의 카드 좌상단 라벨(팀 색 점 + "2팀 · 안개 등대")이다.
    ///
    /// 방의 누구든 고르면 그 사실이 여기 실린다 — 누가 무엇을 골랐는지는 모두가 본다.
    /// 다만 <see cref="Locked"/>은 같은 팀일 때만 참이다. 기획서 9.1의 중복 픽 금지는
    /// 팀 안에서만이라, 다른 팀의 픽까지 잠그면 고를 수 있는 칸이 팀 수만큼 줄어든다.
    public readonly struct Claim
    {
        public readonly int Character;
        public readonly string Label;
        public readonly Color Color;
        public readonly bool Locked;
        public Claim(int character, string label, Color color, bool locked)
        {
            Character = character; Label = label; Color = color; Locked = locked;
        }
    }

    [Header("머리")]
    [SerializeField] TMP_Text waitLabel;
    [SerializeField] TMP_Text timer;

    [Header("카드")]
    [SerializeField] UICharacterCard cardPrefab;
    [SerializeField] RectTransform cardRoot;

    /// 무대는 화면 프리팹의 자식이 될 수 없다. 캔버스 루트의 스케일이 자식에게 그대로
    /// 내려가서 3D 모델까지 같이 줄어든다. 그래서 따로 찍어 씬에 세운다.
    [SerializeField] CharacterStage stagePrefab;

    /// 이름표가 놓이는 판. 화면을 가득 채우고 피벗이 가운데라, 무대 카메라가 준 화면
    /// 좌표를 그대로 옮겨 놓을 수 있다.
    [SerializeField] RectTransform nameplateLayer;

    /// 자리 수만큼 프리팹에 미리 깔아 둔다. 상한이 `CharacterStage.MaxSeats`로 고정이라
    /// 런타임에 만들 이유가 없다.
    [SerializeField] UICharacterNameplate[] nameplates = Array.Empty<UICharacterNameplate>();

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

    /// 팀 칸. 순서가 곧 `UITheme.TeamColors`의 인덱스이고 이름표는 「팀 N」이 된다.
    [SerializeField] UITeamSlot[] teamSlots = Array.Empty<UITeamSlot>();

    [Header("정보 패널 접기")]
    /// 접었다 펴는 대상. 패널 자체이며 옆의 탭은 남는다 — 탭이 같이 사라지면 다시 펼 수가 없다.
    [SerializeField] GameObject detailPanel;

    /// 패널 오른쪽에 붙는 손잡이. 누르면 접히고 다시 누르면 펴진다.
    [SerializeField] Button panelTabButton;

    /// 손잡이에 그리는 화살표. 펴져 있으면 ◀(접기), 접혀 있으면 ▶(펴기)다.
    [SerializeField] TMP_Text panelTabGlyph;

    /// F1과 패드 Y를 읽을 액션 애셋. `MatchFlow`와 같은 애셋을 쓴다 — 새 애셋을 만들지 않는다.
    [SerializeField] InputActionAsset actions;

    /// 패널을 여닫는 액션. 액션 이름 표기는 `MatchFlow`와 같은 방식이다.
    const string TogglePanelActionPath = "UI/TogglePanel";

    /// 참고한 화면이 패드 프롬프트를 쓰므로 키보드 전용으로 두지 않는다. 패널을 영영 못
    /// 펴는 입력 장치가 생기면 안 된다.
    InputAction togglePanel;

    [Header("스크롤 시안 비교")]
    /// 지금 어느 시안인지 보여 주는 우하단 두 번째 힌트. 눌러 볼 키도 여기 적힌다.
    [SerializeField] TMP_Text scrollHintLabel;

    /// 1안(현행 1열)과 D안(1열 + 스크롤)을 오가는 액션. **비교용 임시 장치다.**
    ///
    /// ponytail: 어느 시안으로 갈지가 미결이라 둘 다 남겨 뒀다. 시안이 확정되면 이 액션과
    /// `CharacterStage`의 스크롤 필드를 함께 지운다.
    const string ToggleLayoutActionPath = "UI/ToggleStageLayout";

    /// 스크롤을 미는 축. 새 액션을 만들지 않고 UI 맵에 이미 있는 것을 쓴다.
    const string NavigateActionPath = "UI/Navigate";

    InputAction toggleLayout;
    InputAction navigate;

    [Header("바닥")]
    [SerializeField] Button confirmButton;

    /// 방장은 「게임 시작」, 나머지는 「준비」로 글자가 바뀐다 (`SetLobby`).
    [SerializeField] TMP_Text confirmLabel;

    [SerializeField] Button backButton;

    /// 찍어 낸 카드. 인덱스는 `CharacterCatalog.All`과 같다.
    readonly List<UICharacterCard> cards = new();

    int selected = -1;
    int colorIndex;
    Action<int> onPick;
    Action<int> onColor;

    /// 남이 집어 간 칸. 카드를 다시 칠할 때마다 필요해서 들고 있는다.
    IReadOnlyList<Claim> claims;

    /// 찍어 세운 무대. 씬이 바뀌면 같이 사라지므로 열 때마다 있는지 본다.
    CharacterStage stage;

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
        UIButtons.Wire(panelTabButton, () => TogglePanel());

        // 화면은 재사용되므로 접어 둔 채 나갔어도 다시 들어오면 펴진 상태로 시작한다.
        TogglePanel(true);

        BuildCards();

        BuildTeamSlots();

        SelectColor(Mathf.Clamp(myColor, 0, Mathf.Max(teamSlots.Length - 1, 0)), false);
        RefreshClaims();

        // 열면서 고른 칸도 곧바로 알린다. 알리지 않으면 내 화면에서만 강조된 칸이 남에게는
        // 빈 칸이라, 뒤늦게 들어온 사람이 먼저 있던 사람의 픽을 보지 못한다.
        //
        // 짝꿍의 선택지를 뺏지 않도록 비어 있는 칸부터 고른다 — `Step`이 잠긴 칸을 건너뛴다.
        selected = -1;
        RefreshSelection();
        if (preselect >= 0 && preselect < cards.Count && !LockedAt(preselect)) Select(preselect, true);
        else Step(1);
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
            card.Bind(all[i].Name, ResourceManager.Instance.CrewSprite(all[i].Id),
                      () => Select(index, true));
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
        if (LockedAt(selected)) Step(1);
    }

    /// 방의 명단을 무대에 세운다. 로비가 바뀔 때마다 부른다 — 매 프레임이 아니다.
    ///
    /// 자리는 명단 순서 그대로다. 같은 캐릭터가 이미 서 있는 자리는 다시 세우지 않는다
    /// (`CharacterStage.SetSeat`).
    public void SetRoster(IReadOnlyList<SteamLobby.RoomMember> members)
    {
        if (stage == null || members == null) return;

        var seats = Mathf.Min(members.Count, stage.SeatCount);
        var palette = UITheme.TeamColors;

        stage.Layout();
        stage.FocusSeat(CharacterStage.SelfSeat);

        // 내 자리는 어느 클라이언트에서나 `SelfSeat` 하나다. 남들은 그 자리를 건너뛰고
        // 명단 순서대로 나머지를 채운다 — 그래야 조명 아래가 언제나 나다.
        var next = 0;
        for (var i = 0; i < seats; i++)
        {
            var member = members[i];
            int seat;
            if (member.IsSelf)
            {
                seat = CharacterStage.SelfSeat;

                // 내 팀은 로비가 정답이다. 남이 바꿔 놓았거나 입장하며 배정됐을 수도 있어
                // 화면 상태를 여기서 맞춘다 — 알림 없이 표시만 갈아 끼운다.
                SelectColor(member.Team, false);
            }
            else
            {
                if (next == CharacterStage.SelfSeat) next++;
                seat = next++;
            }
            if (seat >= stage.SeatCount) continue;

            stage.SetSeat(seat, member.Character);
            stage.SetTeam(seat, member.Team);

            if (seat >= nameplates.Length || nameplates[seat] == null) continue;

            var plate = nameplates[seat];
            var color = member.Team >= 0 && member.Team < teamSlots.Length
                ? TeamColors.Of(member.Team) : UITheme.PanelDeep;

            // 방장에게는 준비 개념이 없다 (기획서 10.1). 로비는 방장을 항상 준비로 치므로
            // 걸러 주지 않으면 방장 머리 위에 「준비」가 계속 붙어 있다.
            plate.SetVisible(true);
            plate.Render(member.Name, color, member.IsReady && !member.IsHost, member.IsSelf);
        }

        shownPlates = seats;
        PlaceNameplates();

        // 나간 사람 자리는 모델도 이름표도 치운다.
        stage.ClearFrom(seats);
        for (var i = seats; i < nameplates.Length; i++)
            if (nameplates[i] != null) nameplates[i].SetVisible(false);
    }

    /// 대기실 상태 (기획서 10.1). 방을 만들면 바로 이 화면이라 준비 표시도 여기 붙는다.
    ///
    /// 방장은 시작 버튼을 쥐고 나머지는 준비를 누른다 — 둘이 같은 버튼 자리를 나눠 쓴다.
    public void SetLobby(bool isHost, bool canStart, bool selfReady, int readyNow, int total)
    {
        if (waitLabel != null) waitLabel.text = "준비한 크루";
        if (timer != null) timer.text = $"{readyNow}/{total}";

        if (confirmLabel != null)
            confirmLabel.text = isHost ? "게임 시작" : selfReady ? "준비 취소" : "준비";

        // 방장은 아직 시작할 수 없는 이유(정원 초과·준비 미완)를 잠금으로 본다.
        // 손님의 준비는 언제든 누를 수 있다.
        confirmButton.SetInteractable(!isHost || canStart);
    }

    /// 지금 켜 둔 이름표 수. 카메라가 움직이는 동안 이만큼만 다시 놓는다.
    int shownPlates;

    /// 이름표를 캐릭터 머리 위에 붙인다.
    ///
    /// **매 프레임 돈다.** 시네머신이 감쇠하며 카메라를 옮기므로 한 번 놓고 끝내면
    /// 이름표만 제자리에 남아 캐릭터와 어긋난다. 여기서 하는 일은 좌표 계산뿐이고
    /// 탐색이나 컴포넌트 조회는 없다 — 참조는 전부 미리 잡아 둔 것이다.
    ///
    /// 시네머신은 LateUpdate에서 카메라를 옮기므로 그보다 뒤에 읽어야 한 프레임 안 늦는다.
    void LateUpdate() => PlaceNameplates();

    void PlaceNameplates()
    {
        if (stage == null || nameplateLayer == null) return;

        var count = Mathf.Min(shownPlates, nameplates.Length);
        for (var i = 0; i < count; i++)
        {
            var plate = nameplates[i];
            if (plate == null) continue;

            // Overlay 캔버스라 변환에 카메라를 넘기지 않는다.
            if (stage.TryHeadScreenPoint(i, out var screenPoint)
                && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                       nameplateLayer, screenPoint, null, out var local))
                plate.PlaceAt(local);
        }
    }

    public override void OnShow()
    {
        // 씬이 바뀌면 무대가 함께 사라진다. 없을 때만 한 번 세운다 — 매 프레임이 아니다.
        if (stage == null && stagePrefab != null) stage = Instantiate(stagePrefab);
        if (stage != null) stage.SetActive(true);

        if (actions == null)
        {
            CDebug.LogError($"{name}: {nameof(InputActionAsset)}가 연결되지 않았다. "
                          + "F1·패드 Y로 정보 패널을 접을 수 없다.", this);
            return;
        }

        togglePanel ??= actions.FindAction(TogglePanelActionPath, true);
        togglePanel.performed += OnTogglePanel;
        togglePanel.Enable();

        toggleLayout ??= actions.FindAction(ToggleLayoutActionPath, true);
        toggleLayout.performed += OnToggleLayout;
        toggleLayout.Enable();

        // Navigate는 EventSystem의 입력 모듈도 쓰는 공용 액션이다. 이미 켜져 있으면
        // 그대로 두고, 꺼져 있을 때만 켠다 — 끄는 것은 `OnHide`에서도 하지 않는다.
        navigate ??= actions.FindAction(NavigateActionPath, true);
        if (!navigate.enabled) navigate.Enable();

        RefreshScrollHint();
    }

    public override void OnHide()
    {
        if (stage != null) stage.SetActive(false);

        if (togglePanel != null)
        {
            togglePanel.performed -= OnTogglePanel;
            togglePanel.Disable();
        }
        if (toggleLayout != null)
        {
            toggleLayout.performed -= OnToggleLayout;
            toggleLayout.Disable();
        }
    }

    /// 스크롤을 민다. **스크롤 시안일 때만 돈다.**
    ///
    /// 시네머신이 LateUpdate에서 카메라를 옮기므로 미는 것은 그 전이어야 한다 — LateUpdate에
    /// 두면 이름표 배치(`PlaceNameplates`)와 한 프레임 어긋난다. 여기서 하는 일은 축 하나를
    /// 읽어 넘기는 것뿐이고 탐색이나 컴포넌트 조회는 없다.
    void Update()
    {
        if (stage == null || navigate == null || !stage.ScrollMode) return;
        stage.Pan(navigate.ReadValue<Vector2>().x, Time.deltaTime);
    }

    void OnToggleLayout(InputAction.CallbackContext _)
    {
        if (stage == null) return;
        stage.SetScrollMode(!stage.ScrollMode);
        RefreshScrollHint();
    }

    void RefreshScrollHint()
    {
        if (scrollHintLabel == null) return;
        scrollHintLabel.text = stage != null && stage.ScrollMode ? "스크롤 켬" : "스크롤 끔";
    }

    void OnTogglePanel(InputAction.CallbackContext _) => TogglePanel();

    /// 정보 패널을 접거나 편다. <paramref name="force"/>를 주면 그 상태로 맞춘다.
    ///
    /// 탭의 x는 코드가 정하지 않는다 — 패널과 탭이 한 `HorizontalLayoutGroup` 아래 있어서
    /// 패널이 꺼지면 탭이 그 자리로 따라 붙는다.
    void TogglePanel(bool? force = null)
    {
        if (detailPanel == null) return;

        var open = force ?? !detailPanel.activeSelf;
        detailPanel.SetActive(open);
        if (panelTabGlyph != null) panelTabGlyph.text = open ? "◀" : "▶";
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
            if (at < 0) cards[i].SetClaim(null, default, false);
            else cards[i].SetClaim(claims[at].Label ?? string.Empty, claims[at].Color, LockedAt(i));
        }
    }

    /// 그 칸이 나에게 잠겨 있는가. 같은 칸을 다른 팀도 골랐을 수 있으므로 전부 훑는다 —
    /// 잠그는 것은 같은 팀의 픽 하나뿐이다.
    bool LockedAt(int character)
    {
        if (claims == null) return false;
        for (var i = 0; i < claims.Count; i++)
            if (claims[i].Character == character && claims[i].Locked) return true;
        return false;
    }

    /// 화살표로 선택을 옮긴다. 선점된 칸은 건너뛴다.
    void Step(int delta)
    {
        var count = cards.Count;
        if (count == 0) return;

        for (var n = 1; n <= count; n++)
        {
            var next = ((selected + delta * n) % count + count) % count;
            if (LockedAt(next)) continue;
            Select(next, true);
            return;
        }
    }

    void Select(int index, bool notify)
    {
        var all = CharacterCatalog.All;
        if (index < 0 || index >= all.Length || index >= cards.Count || LockedAt(index)) return;

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
            return colorIndex >= 0 && colorIndex < teamSlots.Length
                ? TeamColors.Of(colorIndex) : UITheme.PanelDeep;
        }
    }

    /// 팀 칸에 이름표와 팀 색을 먹인다. 칸 수는 프리팹이 정하고 색은 팔레트 순서를 따른다.
    void BuildTeamSlots()
    {
        var palette = UITheme.TeamColors;
        for (var i = 0; i < teamSlots.Length; i++)
        {
            if (teamSlots[i] == null) continue;
            var index = i;
            var color = TeamColors.Of(i);
            teamSlots[i].Bind($"팀 {i + 1}", color, () => SelectColor(index, true));
        }
    }

    void SelectColor(int index, bool notify)
    {
        if (index < 0 || index >= teamSlots.Length) return;

        colorIndex = index;
        for (var i = 0; i < teamSlots.Length; i++)
            if (teamSlots[i] != null) teamSlots[i].SetSelected(i == index);

        if (nameplateSwatch != null && index < UITheme.TeamColors.Length)
            nameplateSwatch.color = TeamColors.Of(index);

        // 팀 색이 바뀌면 고른 카드도 따라 바뀐다.
        RefreshSelection();

        if (notify) onColor?.Invoke(index);
    }

    void Set(TMP_Text target, string value)
    {
        if (target != null) target.text = value;
    }
}
