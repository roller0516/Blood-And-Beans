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
