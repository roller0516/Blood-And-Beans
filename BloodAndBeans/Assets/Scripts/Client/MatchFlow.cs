using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;

/// 매치 씬의 UI 조립 지점. 타이틀 씬의 `TitleFlow`와 같은 역할이다.
///
/// 복제 상태를 가진 씬 오브젝트들은 Inspector로 잇는다. 전역 조회로 찾지 않는 이유는
/// 이들이 이 씬에 같이 놓여 있어서 찾을 이유가 없기 때문이다.
public sealed class MatchFlow : MonoBehaviour
{
    [Header("복제 상태")]
    [SerializeField] GamePhase phase;
    [SerializeField] TransitionLedger ledger;

    /// 갱신 주기. 매 프레임 문자열을 새로 만들지 않기 위한 것이다.
    [SerializeField] float refreshInterval = 0.1f;

    /// 귀환 결과 창이 떠 있는 시간. 이 창은 낮이 시작될 때 뜨는데(밤 -> 낮 -> 전환),
    /// 낮은 2분짜리 조작 구간이라 창이 계속 덮고 있으면 안 된다.
    [SerializeField] float returnPopupSeconds = 4f;

    [Header("페이즈 연출")]
    /// 밤이 시작되고 카운트다운이 도는 시간. **이 동안 조작이 막힌다**
    /// (`UIPhaseCuePopup.BlocksPlayerInput`). 숫자는 이 값에서 내려온다 — 3이면 3 · 2 · 1이다.
    // ponytail: 임시값. 기획서 6.4의 밤 흐름표에 시작 카운트다운이 없다. 생기면 그 절로 옮긴다
    [SerializeField] float nightCountdownSeconds = 3f;

    /// 낮 시작 「READY / GO!」가 도는 시간. 절반은 READY, 절반은 GO!다.
    /// 귀환 결과 창이 접힌 뒤에 시작한다 — 낮 시작 순간에 두 연출이 겹치지 않게 한다.
    // ponytail: 임시값. 기획서에 낮 시작 연출 규정이 없다
    [SerializeField] float dayCueSeconds = 1.2f;

    /// 낮이 끝나고 「마감!」이 도는 시간. 이것이 끝나야 정산 화면이 열린다 (기획서 4.1).
    // ponytail: 임시값. 기획서 4.1은 전환 10초만 정하고 그 안의 순서는 정하지 않았다
    [SerializeField] float closingCueSeconds = 1.5f;

    [Header("입력")]
    /// ESC를 읽을 액션 애셋. 플레이어 조작과 같은 애셋이며 새 바인딩을 만들지 않는다 —
    /// `UI/Cancel`에 이미 키보드 Escape와 게임패드 B가 물려 있다.
    [SerializeField] InputActionAsset actions;

    /// 설정 팝업을 여닫는 액션. 액션 이름 표기는 `PlayerInputRouter`와 같은 방식이다.
    const string CancelActionPath = "UI/Cancel";

    /// HUD 조합식 패널을 여닫는 액션. 캐릭터 선택창의 정보 패널과 같은 F1·패드 Y다.
    const string TogglePanelActionPath = "UI/TogglePanel";

    InputAction cancel;
    InputAction togglePanel;
    const string MoveActionPath = "Player/Move";
    const string InteractActionPath = "Player/Interact";
    InputAction recipeMove;
    InputAction recipeInteract;

    MatchHudPresenter presenter;
    UIMatchHudScreen hud;

    /// 루팅 창을 띄운 박스. 서버가 세션을 닫으면 같이 닫는다.
    ItemBox lootBox;
    bool lootOpen;

    /// 그리드 창을 띄운 재료 칸 (기획서 6.5.4). 상자와 같은 창을 쓰지만 낮에만 뜨므로
    /// 둘이 겹치지 않는다.
    IngredientShelf gridShelf;

    /// 내 팀의 복귀 구역. 늦게 복제되므로 아직 없을 때만 한 번 찾는다.
    ReturnZone zone;
    bool returnPopupOpen;
    float returnPopupUntil;

    /// 첫 밤 시작 전 로딩 창. 떠 있을 때만 값이 있다.
    UILoadingPopup loading;

    /// 페이즈 경계 연출 창. 떠 있을 때만 값이 있다.
    UIPhaseCuePopup cue;

    /// 최종 결산을 이미 띄웠는가. 판이 끝나는 것은 한 번뿐이라 다시 열지 않는다.
    bool resultPopupOpen;

    /// 전환 페이즈에 떠 있는 정산 화면. 전환이 끝나면 매치 HUD로 되돌린다.
    UIDaySettlementScreen settlement;

    /// HUD·팝업이 재료 아이콘·가방·캐릭터 초상을 꺼낸다.
    const ResourceManager.SpriteTables Sprites = ResourceManager.SpriteTables.All;

    /// 불러오기를 시작했는가. 시작하지 않은 채 파괴되면 놓을 것도 없다.
    bool acquired;

    async UniTaskVoid Start()
    {
        // 없으면 `Instance`가 만든다. 여기서 만들지 않으므로 씬 배선도 필요 없다.
        var ui = UIManager.Instance;
        if (ui == null)
        {
            CDebug.LogError($"{name}: {nameof(UIManager)}를 얻지 못했다. 매치 HUD를 열 수 없다.", this);
            enabled = false;
            return;
        }

        // HUD와 그 스프라이트를 먼저 불러 둔다. 그동안 Update가 팝업을 먼저 열지 않게 멈춰 둔다.
        enabled = false;
        acquired = true;
        var token = this.GetCancellationTokenOnDestroy();

        // 스프라이트가 먼저다. 뷰 프리팹은 만들어지는 순간 Awake에서 표를 꺼내므로
        // (`UIMatchHudScreen`의 가방 아이콘·메뉴표), 같이 기다리면 표가 비어 있는 채로 읽는다.
        await ResourceManager.Instance.PreloadSpritesAsync(Sprites, token);
        await UniTask.WhenAll(
            ui.LoadAsync<UIMatchHudScreen>(token),
            ui.LoadAsync<UILoadingPopup>(token),
            ui.LoadAsync<UIPhaseCuePopup>(token));
        enabled = true;

        if (phase == null)
        {
            CDebug.LogError($"{name}: {nameof(GamePhase)}가 연결되지 않았다. HUD가 빈 채로 뜬다.", this);
            enabled = false;
            return;
        }

        // Push가 아니라 Replace다. UIManager가 영속이라 타이틀의 화면이 스택에 그대로
        // 남아 있고, 그 위에 얹으면 매치가 끝나고 돌아갈 때 그 화면이 되살아난다.
        var screen = ui.ReplaceScreen<UIMatchHudScreen>();
        if (screen == null)
        {
            enabled = false;
            return;
        }

        hud = screen;
        presenter = new MatchHudPresenter(screen, phase, ledger, refreshInterval);

        BindCancel();
    }

    /// ESC를 설정 팝업에 잇는다. 애셋을 이어 두지 않았으면 팝업을 열 방법이 없다는 뜻이라
    /// 조용히 넘기지 않고 알린다.
    void BindCancel()
    {
        if (actions == null)
        {
            CDebug.LogError($"{name}: {nameof(InputActionAsset)}가 연결되지 않았다. "
                          + "ESC로 설정을 열 수 없다.", this);
            return;
        }

        cancel = actions.FindAction(CancelActionPath, true);
        cancel.performed += OnCancel;
        cancel.Enable();

        togglePanel = actions.FindAction(TogglePanelActionPath, true);
        togglePanel.performed += OnToggleRecipe;
        togglePanel.Enable();
        recipeMove = actions.FindAction(MoveActionPath, true);
        recipeInteract = actions.FindAction(InteractActionPath, true);
        recipeMove.performed += OnRecipeActivity;
        recipeInteract.started += OnRecipeActivity;
    }

    /// F1 한 번에 HUD의 조합식 패널을 펴고 다시 누르면 접는다. 팝업이 떠 있거나 HUD가
    /// 가려져 있으면(정산 화면) 건드리지 않는다.
    void OnToggleRecipe(InputAction.CallbackContext _)
    {
        var ui = UIManager.Instance;
        if (ui == null || hud == null || ui.PopupDepth > 0 || ui.CurrentScreen != hud) return;
        hud.ToggleRecipe();
    }

    void OnRecipeActivity(InputAction.CallbackContext _)
    {
        if (hud != null && hud.RecipeOpen) hud.ToggleRecipe(false);
    }

    /// ESC 한 번에 설정을 열고, 다시 누르면 닫는다.
    ///
    /// 다른 팝업(상자 루팅)이 떠 있으면 아무것도 하지 않는다. 그 창은 서버가 여는 세션에
    /// 붙어 있어서 클라이언트가 닫을 수 있는 것이 아니고, 그 위에 설정을 얹으면 세션이
    /// 끝날 때 `SyncLootPopup`이 맨 위(설정)를 대신 닫는다.
    void OnCancel(InputAction.CallbackContext _)
    {
        var ui = UIManager.Instance;
        if (ui == null) return;

        if (ui.CurrentPopup is UISettingsPopup)
        {
            ui.PopPopup();
            return;
        }

        if (ui.PopupDepth > 0) return;
        if (hud != null && hud.RecipeOpen)
        {
            hud.ToggleRecipe(false);
            return;
        }
        OpenSettingsAsync(ui).Forget();
    }

    /// 처음 여는 순간에는 프리팹을 불러와야 한다. 기다리는 사이 다른 팝업이 떴으면 얹지 않는다.
    async UniTaskVoid OpenSettingsAsync(UIManager ui)
    {
        await ui.LoadAsync<UISettingsPopup>(this.GetCancellationTokenOnDestroy());
        if (ui.PopupDepth > 0) return;
        var popup = ui.PushPopup<UISettingsPopup>();
        popup?.Bind(ui.PopPopup);
    }

    /// 매치 씬이 내려갈 때 자기 화면과 팝업을 치운다. UIManager는 씬과 함께 죽지 않으므로
    /// 여기서 치우지 않으면 타이틀로 돌아가서도 매치 HUD가 스택에 남는다.
    void OnDestroy()
    {
        if (cancel != null) cancel.performed -= OnCancel;
        if (togglePanel != null) togglePanel.performed -= OnToggleRecipe;
        if (recipeMove != null) recipeMove.performed -= OnRecipeActivity;
        if (recipeInteract != null) recipeInteract.started -= OnRecipeActivity;

        if (!acquired) return;
        ResourceManager.Instance.ReleaseSprites(Sprites);

        var ui = UIManager.Instance;
        if (ui == null) return;

        ui.PopAllPopups();
        ui.ClearScreens();
        ui.UnloadUnused();
    }

    void Update()
    {
        presenter?.Tick(Time.unscaledTime);
        if (hud != null && hud.RecipeOpen && recipeMove != null
            && recipeMove.ReadValue<Vector2>() != Vector2.zero)
            hud.ToggleRecipe(false);

        // 게이지는 0.6초 남짓이라 HUD 갱신 주기(0.1초)로 그리면 여섯 칸짜리 계단이 된다.
        // 문자열을 만들지 않는 스케일 대입 하나라 매 프레임 불러도 된다.
        if (hud != null && presenter != null)
        {
            hud.SetCastProgress(presenter.CastProgress01);

            // 귀환 마커도 같은 이유로 매 프레임이다. 월드의 한 점에 붙어 있어서
            // 0.1초마다 옮기면 카메라가 도는 동안 끊겨 보인다 (기획서 6.4).
            hud.SetReturnMarker(presenter.Marker);

            // 완성 게이지도 매 프레임이다. 침이 초당 1.4회 왕복해서 0.1초마다 옮기면
            // 노릴 수 없는 계단이 된다 (기획서 5.2).
            hud.SetCompletionGauge(default);
        }

        SyncLoadingPopup();
        SyncLootPopup();
        SyncShelfPopup();
        SyncReturnPopup();
        SyncPhaseCue();
        SyncSettlementScreen();
        SyncResultPopup();
    }

    /// 방 인원이 다 모여 첫 밤이 시작될 때까지 로딩 창으로 덮는다. 모이는 시점은 서버가 정한다.
    /// 시작 전에는 다른 팝업이 뜰 일이 없어 이 창이 항상 맨 위다.
    void SyncLoadingPopup()
    {
        var ui = UIManager.Instance;
        if (ui == null) return;

        var waiting = phase == null || !phase.IsSpawned || !phase.Started;
        if (!waiting)
        {
            if (loading == null) return;
            if (ui.CurrentPopup == loading) ui.PopPopup();
            loading = null;
            return;
        }

        if (loading == null) loading = ui.PushPopup<UILoadingPopup>();
        if (loading == null) return;     // 아직 불러오는 중이면 다음 프레임에 다시 연다

        var spawned = phase != null && phase.IsSpawned;
        loading.SetProgress(spawned ? phase.JoinedPlayers : 0, spawned ? phase.ExpectedPlayers : 0);
    }

    /// 페이즈가 바뀌는 순간을 화면 한복판에서 알린다 — 밤 시작 카운트다운 · 낮 시작
    /// 「READY / GO!」 · 낮 마감 「마감!」 셋이다.
    ///
    /// **전이 이벤트를 받아 타이머를 돌리지 않고 복제된 경과 시간에서 매 프레임 계산한다.**
    /// 이벤트로 재면 클라이언트마다 자기 시계로 세게 되어 카운트다운 숫자가 갈리고, 늦게
    /// 붙은 손님은 그 순간을 아예 놓친다. 시계는 서버 것 하나뿐이다 (`GamePhase.Elapsed`).
    /// 늦게 들어온 클라이언트에게 연출이 짧게 보이는 것은 이 설계의 결과이지 결함이 아니다.
    void SyncPhaseCue()
    {
        var ui = UIManager.Instance;
        if (ui == null) return;

        if (phase == null || !phase.IsSpawned || !phase.Started || phase.Finished)
        {
            CloseCue(ui);
            return;
        }

        var elapsed = phase.Elapsed;

        // 밤 시작. 이 구간만 조작을 막는다 (`UIPhaseCuePopup.BlocksPlayerInput`).
        if (phase.Current == Phase.Night && elapsed < nightCountdownSeconds)
        {
            OpenCue(ui)?.ShowCount(Mathf.Max(1, Mathf.CeilToInt(nightCountdownSeconds - elapsed)));
            return;
        }

        // 낮 시작. 귀환 결과 창(6.8)이 접힌 뒤에 온다 — 낮이 열리는 순간에 두 연출이
        // 겹치면 어느 쪽도 읽히지 않는다.
        if (phase.Current == Phase.Day && !returnPopupOpen
            && elapsed >= returnPopupSeconds
            && elapsed < returnPopupSeconds + dayCueSeconds)
        {
            var popup = OpenCue(ui);
            if (popup == null) return;
            if (elapsed < returnPopupSeconds + dayCueSeconds * 0.5f) popup.ShowReady();
            else popup.ShowGo();
            return;
        }

        // 낮 마감. 정산 화면은 이것이 끝난 뒤에 열린다 (`SyncSettlementScreen`).
        if (phase.Current == Phase.Transition && elapsed < closingCueSeconds)
        {
            OpenCue(ui)?.ShowClosing();
            return;
        }

        CloseCue(ui);
    }

    /// 아직 불러오는 중이면 null이고 다음 프레임에 다시 연다. 입력 게이트는 `PushPopup`이
    /// 맞추고, 떠 있는 채로 막기가 바뀌는 것은 창이 스스로 알린다 (`UIPhaseCuePopup`).
    UIPhaseCuePopup OpenCue(UIManager ui)
    {
        // `??=`를 쓰지 않는다. 파괴된 UnityEngine.Object는 `null` 병합 연산자에게 null이
        // 아니어서, 창이 언로드된 뒤로 영영 다시 열리지 않는다.
        if (cue == null) cue = ui.PushPopup<UIPhaseCuePopup>();
        return cue;
    }

    /// 위에 다른 창이 얹혀 있으면 접지 않는다. `PopPopup`은 맨 위를 닫으므로 남의 창을
    /// 대신 닫게 된다 — 그 창이 먼저 닫히면 다음 프레임에 여기로 다시 온다.
    void CloseCue(UIManager ui)
    {
        if (cue == null || ui.CurrentPopup != cue) return;

        ui.PopPopup();       // 입력 게이트를 여기서 다시 맞춘다
        cue = null;
    }

    /// 전환 페이즈(10초) 동안 정산 화면을 띄운다 (기획서 4장: 매출/임대료 결과 · 순위 ·
    /// 내일의 손님 예보).
    ///
    /// 값의 출처는 전부 복제된 것이다. 매출과 순위는 판에 하나뿐인 `Scoreboard`,
    /// 임대료·부채·미납 횟수는 `TransitionLedger`가 마감 순간 자기 팀에만 보낸 요약,
    /// 예보는 같은 컴포넌트가 밤 끝에 보낸 두 요약이다. 클라이언트는 아무것도 계산하지 않는다.
    ///
    /// 화면 스택을 `Replace`하지 않고 `Push`하는 이유는 전환이 끝나면 밑에 깔린 매치
    /// HUD로 그대로 돌아와야 하기 때문이다.
    void SyncSettlementScreen()
    {
        var ui = UIManager.Instance;
        if (ui == null || phase == null || !phase.IsSpawned) return;

        // 첫 전환(밤 → 낮 1일차)에는 아직 마감된 하루가 없다. 그때 정산 화면을 띄우면
        // 청구되지도 않은 1일차 임대료를 미납으로, 매출을 0으로 그린다 — 임대료는 낮이
        // 끝날 때 청구된다 (기획서 3.2). 마감 결과가 온 뒤에만 연다.
        var settled = ledger != null && ledger.Today.Valid;

        // 「마감!」 연출이 끝난 뒤에 연다 (`SyncPhaseCue`). 동시에 열면 정산 화면이 연출을
        // 덮어 마감을 알리는 순간이 사라진다.
        var cueDone = phase.Elapsed >= closingCueSeconds;
        var inTransition = phase.Current == Phase.Transition && !phase.Finished && settled && cueDone;

        if (!inTransition)
        {
            if (settlement == null) return;
            ui.PopScreen();
            settlement = null;
            return;
        }

        if (settlement == null)
        {
            settlement = ui.PushScreen<UIDaySettlementScreen>();
            if (settlement == null) return;      // 아직 불러오는 중이면 다음 프레임에 다시 연다
            BindSettlement();
        }

        settlement.SetRemaining(phase.Remaining, phase.Duration(Phase.Transition));
    }

    /// 이 팀의 카페. 자기 팀 것만 복제되므로(`MatchDirector.SpawnCafesServer`) 여기서
    /// 얻는 것은 언제나 내 카페다.
    Cafe LocalCafe
    {
        get
        {
            var director = MatchDirector.Instance;
            return director != null ? director.CafeOf(PlayerTeam.Local()) : null;
        }
    }

    void BindSettlement()
    {
        var director = MatchDirector.Instance;
        var board = director != null ? director.Board : null;
        var team = PlayerTeam.Local();

        var today = ledger != null ? ledger.Today : default;
        var day = today.Valid ? today.Day : phase.Day;

        // 순위. 공개되는 것은 매출뿐이다 (기획서 3.1). 카페 이름이 데이터에 없어 팀 번호로 쓴다.
        var standings = new List<UIDaySettlementScreen.StandingRow>();
        if (board != null)
            for (var t = 0; t < board.TeamCount; t++)
                standings.Add(new UIDaySettlementScreen.StandingRow(
                    DisplayNames.Team(t), board.RevenueOf(t),
                    t == team && today.Valid ? today.Sales : 0, t == team));
        standings.Sort((a, b) => b.Total.CompareTo(a.Total));

        // 예보. 종족별 인원수와 인기 재료만 온다 (기획서 5.6.3).
        var guests = new List<UIDaySettlementScreen.GuestCard>();
        var counts = ledger != null ? ledger.RaceCounts : null;
        if (counts != null)
            // 0마리 종족은 내보내지 않는다. 예보 칸이 6개뿐이라 「x0」 카드가 자리를 차지하면
            // 실제로 오는 구성이 밀려 안 보인다 (기획서 5.6은 등장 종족만 나열한다).
            for (var r = 0; r < counts.Length; r++)
                if (counts[r] > 0)
                    guests.Add(new UIDaySettlementScreen.GuestCard(
                        DisplayNames.Of((Race)r), counts[r]));

        var popular = new List<UIDaySettlementScreen.PopularItem>();
        var shown = ledger != null ? ledger.PopularShown : null;
        if (shown != null)
            foreach (var item in shown)
                popular.Add(new UIDaySettlementScreen.PopularItem(
                    DisplayNames.Of(item),
                    Mathf.RoundToInt(SalePrice.PopularBonus * 100f)));

        settlement.Bind(
            day,
            TradeLines(today),
            today.Valid ? today.Sales : 0,
            today.Valid ? today.RentOwed : Rent.Due(day),
            today.Valid ? today.RentPaid : 0,
            today.Valid ? today.Debt : 0,
            Rent.Due(day + 1),
            standings, guests, popular,
            GemRows(),
            today.Valid ? today.MissStreak : 0,
            PenaltyStages);
    }

    /// 적용 중인 보석 (기획서 4.1 「자동 업그레이드」). 남은 턴은 다음 낮 기준이다 —
    /// 전환 시점의 낮은 이미 끝났고, 플레이어가 알고 싶은 것은 앞으로 남은 몫이다.
    List<UIDaySettlementScreen.GemRow> GemRows()
    {
        var rows = new List<UIDaySettlementScreen.GemRow>();
        var cafe = LocalCafe;
        if (cafe == null) return rows;

        var sprites = ResourceManager.Instance;
        foreach (var gem in Gems.All)
        {
            var turns = cafe.GemTurnsNextDay(gem);
            if (turns <= 0) continue;
            rows.Add(new UIDaySettlementScreen.GemRow(
                sprites.IngredientSprite(Gems.ItemOf(gem)),
                Gems.NameOf(gem), Gems.EffectOf(gem), turns, cafe.GemRefreshed(gem)));
        }
        return rows;
    }

    /// 오늘의 거래 내역. 지금 복제되는 것은 합계뿐이라 한 줄이다 — 판매 잔 수와 판정
    /// 내역은 서버에만 있고 아직 내려오지 않는다.
    static List<UIDaySettlementScreen.TradeLine> TradeLines(TransitionLedger.Settlement s)
    {
        var lines = new List<UIDaySettlementScreen.TradeLine>();
        if (!s.Valid) return lines;
        lines.Add(new UIDaySettlementScreen.TradeLine(
            "오늘 판매", $"+{s.Sales:N0}", UITheme.GoldLit));
        return lines;
    }

    /// 기획서 3.3 표. 화면이 아니라 여기서 넘긴다 — 표의 내용은 규칙이지 표시가 아니다.
    static readonly UIDaySettlementScreen.PenaltyStage[] PenaltyStages =
    {
        new("1회", "제작 속도 -10%", "시야 반경 -15%"),
        new("2회 연속", "제작 -15% + 이동 속도 -10%",
                        "시야 -25% + 박스 개봉 속도 -20%"),
        new("3회 연속", "제작 -20% + 이동 속도 -20%",
                        "시야 -35% + 개봉 -30% + 무게 감속 구간 한 단계 불리하게"),
    };

    /// 판이 끝나면 최종 결산을 띄운다 (기획서 3.1: 마지막 낮이 끝나면 최종 결산, 1위 팀 승리).
    ///
    /// `GamePhase`는 마지막 낮에서 `finished`만 세우고 멈춘다. 그 사실을 화면으로 옮기는
    /// 곳이 없어서, 판이 끝나도 HUD가 "종료 --:--"로 굳는 것이 전부였다.
    ///
    /// 매출판은 판에 하나뿐이고 모든 팀에 복제된다 (`MatchDirector.Board`). 카페에 매달린
    /// 값을 읽으면 자기 팀 매출밖에 못 봐서 순위를 만들 수 없다.
    void SyncResultPopup()
    {
        if (resultPopupOpen || phase == null || !phase.IsSpawned || !phase.Finished) return;

        var ui = UIManager.Instance;
        if (ui == null) return;

        var director = MatchDirector.Instance;
        var board = director != null ? director.Board : null;

        // 매출판이 아직 복제되지 않았으면 다음 프레임에 다시 본다. 빈 목록으로 띄우면
        // 모두가 0G 공동 1위인 결산이 뜬다.
        if (board == null || board.TeamCount == 0) return;

        var revenue = new List<int>(board.TeamCount);
        for (var team = 0; team < board.TeamCount; team++) revenue.Add(board.RevenueOf(team));

        var popup = ui.PushPopup<UIMatchResultPopup>();
        if (popup == null) return;      // 아직 불러오는 중이면 다음 프레임에 다시 연다

        // 로비 복귀는 `SteamLobby.LeaveRoom`이 씬 전환까지 처리한다. 판이 끝나는 것은
        // 한 번뿐이라 여기서 한 번 찾는다 — 주기 실행이 아니다 (AGENTS.md).
        var lobby = FindFirstObjectByType<SteamLobby>();

        // ponytail: "한 판 더"는 재시작 경로가 없어 넘기지 않는다. 팝업이 그 버튼을
        // 잠근다. 매치 재시작이 생기면 여기에 이어 준다.
        popup.Bind(phase.Day, revenue, PlayerTeam.Local(), null,
                   lobby != null ? lobby.ReturnToRoom : (System.Action)null, null);
        resultPopupOpen = true;
    }

    /// 밤이 끝나면 자기 귀환 결과를 창으로 알린다 (기획서 6.8). 판정은 낮이 시작될 때
    /// 서버가 한다 (`ReturnZone`).
    ///
    /// 루팅 창과 같은 방식이다. 결과는 서버가 자기 것만 보내 주고(`ReturnZone`),
    /// 여기서는 아직 소비하지 않은 결과가 있는지만 본다.
    void SyncReturnPopup()
    {
        var ui = UIManager.Instance;
        if (ui == null || phase == null || !phase.IsSpawned) return;

        if (zone == null)
        {
            var director = MatchDirector.Instance;
            var cafe = director != null ? director.CafeOf(PlayerTeam.Local()) : null;
            zone = cafe != null ? cafe.Zone : null;
            if (zone == null) return;
        }

        // 낮이 시작될 때 떠서 `returnPopupSeconds`만큼만 머문다. 낮을 벗어나면 그 전에 접는다.
        if (returnPopupOpen &&
            (phase.Current != Phase.Day || Time.unscaledTime >= returnPopupUntil))
        {
            ui.PopPopup();
            returnPopupOpen = false;
            return;
        }

        if (returnPopupOpen || !zone.HasResult) return;

        // 창이 아직 불러오는 중이면 결과를 남겨 두고 다음 프레임에 다시 연다.
        // 불러오기에 실패한 창은 UIManager가 한 번 알리고 계속 null을 주므로, 그때는 소비하고 넘긴다.
        var popup = ui.PushPopup<UIReturnResultPopup>();
        if (popup == null)
        {
            if (ui.FailedToLoad<UIReturnResultPopup>()) zone.ConsumeResult();
            return;
        }

        popup.Bind(zone.Outcome, zone.KeptCount, zone.LostCount, zone.LossPercent);
        popup.PlayToast(returnPopupSeconds);
        zone.ConsumeResult();
        returnPopupOpen = true;
        returnPopupUntil = Time.unscaledTime + returnPopupSeconds;
    }

    /// 개봉 게이지가 다 차면 창을 열고, 서버가 세션을 닫으면(이동·피격·밤 종료) 닫는다.
    /// F를 놓는 것으로는 닫히지 않는다 — 창은 캐스팅이 아니라 세션에 붙어 있다.
    ///
    /// 어떤 박스를 열었는지는 `PlayerInteract`가 이미 알고 있으므로 여기서 씬을 다시
    /// 뒤지지 않는다.
    void SyncLootPopup()
    {
        var ui = UIManager.Instance;
        if (ui == null) return;

        var night = phase != null && phase.IsSpawned && phase.Current == Phase.Night;
        var candidate = night ? presenter?.BoxHold?.LootBox : null;

        // 파괴된 박스와 아직 열리지 않은 박스는 창을 띄우지 않는다.
        var box = candidate != null && candidate.Opened ? candidate : null;
        if (ReferenceEquals(box, lootBox)) return;

        if (lootOpen)
        {
            ui.PopPopup();
            lootOpen = false;
        }
        if (box == null)
        {
            lootBox = null;
            return;
        }

        // 창이 아직 불러오는 중이면 박스를 기억하지 않는다. 다음 프레임에 같은 박스로 다시 연다.
        var popup = ui.PushPopup<UIBoxLootPopup>();
        if (popup == null) return;
        lootBox = box;

        var hold = presenter.BoxHold;
        popup.Bind(box, hold.TakeSlotClient, presenter.Bag,
                   hud != null ? hud.BagAnchor : null);
        lootOpen = true;
    }

    /// 낮의 재료 칸도 같은 그리드 창에서 꺼낸다 (기획서 6.5.4).
    ///
    /// 창은 F로 열고 F로 닫는다(`IngredientShelf.BeginInteractionClient`). 상자와 달리
    /// 서버 세션이 없으므로 여기서 닫을 조건을 본다 — 낮이 끝나거나 손이 닿지 않을
    /// 만큼 멀어지면 내린다. 멀어져서 내릴 때는 칸의 토글도 함께 꺼야 다시 다가왔을 때
    /// 저절로 열리지 않는다.
    void SyncShelfPopup()
    {
        var ui = UIManager.Instance;
        if (ui == null) return;

        var day = phase != null && phase.IsSpawned && phase.Current == Phase.Day;
        var shelf = day ? presenter?.Interactor?.Latest as IngredientShelf : null;

        if (shelf != null && !shelf.GridOpen) shelf = null;
        if (shelf != null && !shelf.LocalPlayerNear)
        {
            shelf.CloseGridClient();
            shelf = null;
        }

        if (ReferenceEquals(shelf, gridShelf)) return;

        if (gridShelf != null)
        {
            gridShelf.CloseGridClient();
            ui.PopPopup();
        }

        gridShelf = null;
        if (shelf == null) return;

        var popup = ui.PushPopup<UIBoxLootPopup>();
        if (popup == null)
        {
            // 불러오는 중이면 다음 프레임에 다시 연다. 실패한 창이면 토글을 꺼서 매번 시도하지 않게 한다.
            if (ui.FailedToLoad<UIBoxLootPopup>()) shelf.CloseGridClient();
            return;
        }
        gridShelf = shelf;

        // 가방은 밤의 물건이다. 낮에는 손으로 옮기므로 무게 표시도 연출도 없다.
        popup.Bind(shelf, shelf.TakeSlotClient, null, null);
    }
}
