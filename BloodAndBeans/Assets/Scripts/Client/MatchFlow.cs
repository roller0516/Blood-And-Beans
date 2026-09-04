using System.Collections.Generic;
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

    [Header("입력")]
    /// ESC를 읽을 액션 애셋. 플레이어 조작과 같은 애셋이며 새 바인딩을 만들지 않는다 —
    /// `UI/Cancel`에 이미 키보드 Escape와 게임패드 B가 물려 있다.
    [SerializeField] InputActionAsset actions;

    /// 설정 팝업을 여닫는 액션. 액션 이름 표기는 `PlayerInputRouter`와 같은 방식이다.
    const string CancelActionPath = "UI/Cancel";

    InputAction cancel;

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

    /// 최종 결산을 이미 띄웠는가. 판이 끝나는 것은 한 번뿐이라 다시 열지 않는다.
    bool resultPopupOpen;

    /// 전환 페이즈에 떠 있는 정산 화면. 전환이 끝나면 매치 HUD로 되돌린다.
    UIDaySettlementScreen settlement;

    /// 정산 위에 겹쳐 뜨는 설비 업그레이드 화면 (기획서 8장). 전환 페이즈에만 존재한다.
    UIFacilityUpgradeScreen upgrades;

    /// 업그레이드 화면을 이번 전환에서 닫았는가. 「적용」을 눌러 닫은 뒤 같은 전환에서
    /// 다시 뜨면 정산을 볼 수가 없다.
    bool upgradesDismissed;

    void Start()
    {
        // 없으면 `Instance`가 만든다. 여기서 만들지 않으므로 씬 배선도 필요 없다.
        var ui = UIManager.Instance;
        if (ui == null)
        {
            CDebug.LogError($"{name}: {nameof(UIManager)}를 얻지 못했다. 매치 HUD를 열 수 없다.", this);
            enabled = false;
            return;
        }

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

        var popup = ui.PushPopup<UISettingsPopup>();
        popup?.Bind(ui.PopPopup);
    }

    /// 매치 씬이 내려갈 때 자기 화면과 팝업을 치운다. UIManager는 씬과 함께 죽지 않으므로
    /// 여기서 치우지 않으면 타이틀로 돌아가서도 매치 HUD가 스택에 남는다.
    void OnDestroy()
    {
        if (cancel != null) cancel.performed -= OnCancel;

        var cafe = LocalCafe;
        if (cafe != null) cafe.UpgradesChanged -= OnUpgradesReplicated;

        var ui = UIManager.Instance;
        if (ui == null) return;

        ui.UnloadPopups();
        ui.ClearScreens();
    }

    void Update()
    {
        presenter?.Tick(Time.unscaledTime);

        // 게이지는 0.6초 남짓이라 HUD 갱신 주기(0.1초)로 그리면 여섯 칸짜리 계단이 된다.
        // 문자열을 만들지 않는 스케일 대입 하나라 매 프레임 불러도 된다.
        if (hud != null && presenter != null)
        {
            hud.SetCastProgress(presenter.CastProgress01);

            // 귀환 마커도 같은 이유로 매 프레임이다. 월드의 한 점에 붙어 있어서
            // 0.1초마다 옮기면 카메라가 도는 동안 끊겨 보인다 (기획서 6.4).
            hud.SetReturnMarker(presenter.Marker);
        }

        SyncLootPopup();
        SyncShelfPopup();
        SyncReturnPopup();
        SyncSettlementScreen();
        SyncUpgradeScreen();
        SyncResultPopup();
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
        var inTransition = phase.Current == Phase.Transition && !phase.Finished && settled;

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
            if (settlement == null) return;      // 프리팹 미연결은 UIManager가 알린다
            BindSettlement();
        }

        settlement.SetRemaining(phase.Remaining, phase.Duration(Phase.Transition));
    }

    /// 전환 페이즈에 설비 업그레이드 화면을 정산 위로 띄운다 (기획서 4장: 전환은 정산 ·
    /// 순위 · **업그레이드 적용** · 예보를 함께 처리한다).
    ///
    /// 업그레이드 재료가 하나도 없고 설치한 것도 없으면 띄우지 않는다. 10초짜리 구간에서
    /// 아무것도 할 수 없는 화면이 정산을 덮으면 그 10초가 통째로 사라진다 — 이 재료는
    /// 3등급 상자에서만 나와서(기획서 8장) 없는 판이 대부분이다.
    void SyncUpgradeScreen()
    {
        var ui = UIManager.Instance;
        if (ui == null || phase == null || !phase.IsSpawned) return;

        var open = phase.Current == Phase.Transition && !phase.Finished
                && settlement != null && !upgradesDismissed && HasAnythingToShow;

        if (!open)
        {
            if (upgrades != null)
            {
                ui.PopScreen();
                upgrades = null;
            }

            // 전환을 벗어났다. 다음 전환에서는 다시 뜬다.
            if (phase.Current != Phase.Transition) upgradesDismissed = false;
            return;
        }

        if (upgrades == null)
        {
            upgrades = ui.PushScreen<UIFacilityUpgradeScreen>();
            if (upgrades == null) return;        // 프리팹 미연결은 UIManager가 알린다
            BindUpgrades();
        }

        upgrades.SetRemaining(phase.Remaining);
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

    /// 볼 것이 있는가 — 쓸 재료가 있거나 이미 설치한 설비가 있다.
    bool HasAnythingToShow
    {
        get
        {
            var cafe = LocalCafe;
            if (cafe == null) return false;
            return cafe.UpgradeMask != 0 || AvailableParts > 0;
        }
    }

    int AvailableParts
    {
        get
        {
            var stock = LocalCafe?.Stock;
            return stock != null ? stock.CountOf(Ingredient.UpgradePart) : 0;
        }
    }

    void BindUpgrades()
    {
        var cafe = LocalCafe;
        var mask = cafe != null ? cafe.UpgradeMask : 0;

        var installed = new bool[UpgradeCatalog.All.Length];
        for (var i = 0; i < installed.Length; i++)
            installed[i] = TeamUpgrades.AtInMask(mask, i);

        upgrades.Bind(installed, AvailableParts, InstallUpgrade, DismissUpgrades);
    }

    /// 카드를 눌렀다. 재료 차감과 설치 판정은 전부 서버가 한다 (`Cafe.InstallUpgradeRpc`).
    /// 여기서는 눌렸다는 사실만 넘기고, 결과가 복제되면 화면을 다시 그린다.
    void InstallUpgrade(UpgradeId id)
    {
        var cafe = LocalCafe;
        if (cafe == null) return;

        cafe.UpgradesChanged -= OnUpgradesReplicated;
        cafe.UpgradesChanged += OnUpgradesReplicated;
        cafe.InstallUpgradeRpc((int)id);
    }

    void OnUpgradesReplicated()
    {
        if (upgrades != null) BindUpgrades();
    }

    /// 「적용」을 눌러 정산으로 돌아간다. 이번 전환에서는 다시 뜨지 않는다.
    void DismissUpgrades() => upgradesDismissed = true;

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
            today.Valid ? today.MissStreak : 0,
            PenaltyStages);
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
        new("1회", "제작 속도 10% 감소", "시야 반경 감소"),
        new("2회 연속", "커피 머신 1대 불통 (2대 → 1대)",
                        "시야 반경 감소 + 박스 개봉 속도 감소"),
        new("3회 연속", "머신 1대 불통 + 그릇 1개 파손",
                        "위 + 무게 감속 구간이 한 단계 불리하게"),
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
        if (popup == null) return;      // 프리팹 미연결은 UIManager가 오류로 알린다

        // 로비 복귀는 `SteamLobby.LeaveRoom`이 씬 전환까지 처리한다. 판이 끝나는 것은
        // 한 번뿐이라 여기서 한 번 찾는다 — 주기 실행이 아니다 (AGENTS.md).
        var lobby = FindFirstObjectByType<SteamLobby>();

        // ponytail: "한 판 더"는 재시작 경로가 없어 넘기지 않는다. 팝업이 그 버튼을
        // 잠근다. 매치 재시작이 생기면 여기에 이어 준다.
        popup.Bind(phase.Day, revenue, PlayerTeam.Local(), null,
                   lobby != null ? lobby.LeaveRoom : (System.Action)null, null);
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

        // 창을 못 띄우더라도 소비한다. 남겨 두면 매 프레임 다시 시도한다.
        var outcome = zone.Outcome;
        var kept = zone.KeptCount;
        var lost = zone.LostCount;
        var lossPercent = zone.LossPercent;
        zone.ConsumeResult();

        var popup = ui.PushPopup<UIReturnResultPopup>();
        if (popup == null) return;      // 프리팹 미연결은 UIManager가 오류로 알린다

        popup.Bind(outcome, kept, lost, lossPercent);
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

        lootBox = box;

        if (lootOpen)
        {
            ui.PopPopup();
            lootOpen = false;
        }
        if (box == null) return;

        var popup = ui.PushPopup<UIBoxLootPopup>();
        if (popup == null) return;      // 프리팹 미연결은 UIManager가 오류로 알린다

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

        gridShelf = shelf;
        if (shelf == null) return;

        var popup = ui.PushPopup<UIBoxLootPopup>();
        if (popup == null)              // 프리팹 미연결은 UIManager가 오류로 알린다
        {
            gridShelf = null;
            shelf.CloseGridClient();
            return;
        }

        // 가방은 밤의 물건이다. 낮에는 손으로 옮기므로 무게 표시도 연출도 없다.
        popup.Bind(shelf, shelf.TakeSlotClient, null, null);
    }
}
