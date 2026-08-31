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

    [Header("입력")]
    /// ESC를 읽을 액션 애셋. 플레이어 조작과 같은 애셋이며 새 바인딩을 만들지 않는다 —
    /// `UI/Cancel`에 이미 키보드 Escape와 게임패드 B가 물려 있다.
    [SerializeField] InputActionAsset actions;

    /// 설정 팝업을 여닫는 액션. 액션 이름 표기는 `PlayerInputRouter`와 같은 방식이다.
    const string CancelActionPath = "UI/Cancel";

    InputAction cancel;

    MatchHudPresenter presenter;
    MatchHudScreen hud;

    /// 루팅 창을 띄운 박스. 서버가 세션을 닫으면 같이 닫는다.
    ItemBox lootBox;
    bool lootOpen;

    /// 내 팀의 복귀 구역. 늦게 복제되므로 아직 없을 때만 한 번 찾는다.
    ReturnZone zone;
    bool returnPopupOpen;

    /// 최종 결산을 이미 띄웠는가. 판이 끝나는 것은 한 번뿐이라 다시 열지 않는다.
    bool resultPopupOpen;

    /// 전환 페이즈에 떠 있는 정산 화면. 전환이 끝나면 매치 HUD로 되돌린다.
    UIDaySettlementScreen settlement;

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
        var screen = ui.ReplaceScreen<MatchHudScreen>();
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

        if (ui.CurrentPopup is SettingsPopup)
        {
            ui.PopPopup();
            return;
        }

        if (ui.PopupDepth > 0) return;

        var popup = ui.PushPopup<SettingsPopup>();
        popup?.Bind(ui.PopPopup);
    }

    /// 매치 씬이 내려갈 때 자기 화면과 팝업을 치운다. UIManager는 씬과 함께 죽지 않으므로
    /// 여기서 치우지 않으면 타이틀로 돌아가서도 매치 HUD가 스택에 남는다.
    void OnDestroy()
    {
        if (cancel != null) cancel.performed -= OnCancel;

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
        SyncReturnPopup();
        SyncSettlementScreen();
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

        var inTransition = phase.Current == Phase.Transition && !phase.Finished;

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
                    $"{t + 1}팀", board.RevenueOf(t),
                    t == team && today.Valid ? today.Sales : 0, t == team));
        standings.Sort((a, b) => b.Total.CompareTo(a.Total));

        // 예보. 종족별 인원수와 인기 재료만 온다 (기획서 5.6.3).
        var guests = new List<UIDaySettlementScreen.GuestCard>();
        var counts = ledger != null ? ledger.RaceCounts : null;
        if (counts != null)
            for (var r = 0; r < counts.Length; r++)
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

    /// 밤이 끝나면 자기 귀환 결과를 창으로 알린다 (기획서 6.8).
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

        // 전환 10초 동안만 띄운다. 낮이 시작되면 접는다.
        if (returnPopupOpen && phase.Current != Phase.Transition)
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

        var popup = ui.PushPopup<BoxLootPopup>();
        if (popup == null) return;      // 프리팹 미연결은 UIManager가 오류로 알린다

        popup.Bind(box, presenter.BoxHold, hud != null ? hud.BagAnchor : null);
        lootOpen = true;
    }
}
