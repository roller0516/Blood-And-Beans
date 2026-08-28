using UnityEngine;

/// 매치 씬의 UI 조립 지점. 타이틀 씬의 `TitleFlow`와 같은 역할이다.
///
/// 복제 상태를 가진 씬 오브젝트들은 Inspector로 잇는다. 전역 조회로 찾지 않는 이유는
/// 이들이 이 씬에 같이 놓여 있어서 찾을 이유가 없기 때문이다.
public sealed class MatchFlow : MonoBehaviour
{
    [SerializeField] UIManager ui;

    [Header("복제 상태")]
    [SerializeField] GamePhase phase;
    [SerializeField] TransitionLedger ledger;

    /// 갱신 주기. 매 프레임 문자열을 새로 만들지 않기 위한 것이다.
    [SerializeField] float refreshInterval = 0.1f;

    MatchHudPresenter presenter;
    MatchHudScreen hud;

    /// 루팅 창을 띄운 박스. 서버가 세션을 닫으면 같이 닫는다.
    ItemBox lootBox;
    bool lootOpen;

    void Start()
    {
        if (ui == null)
        {
            Debug.LogError($"{name}: UIManager가 연결되지 않았다. 매치 HUD를 열 수 없다.", this);
            enabled = false;
            return;
        }

        if (phase == null)
        {
            Debug.LogError($"{name}: {nameof(GamePhase)}가 연결되지 않았다. HUD가 빈 채로 뜬다.", this);
            enabled = false;
            return;
        }

        var screen = ui.PushScreen<MatchHudScreen>();
        if (screen == null)
        {
            enabled = false;
            return;
        }

        hud = screen;
        presenter = new MatchHudPresenter(screen, phase, ledger, refreshInterval);
    }

    void Update()
    {
        presenter?.Tick(Time.unscaledTime);

        // 게이지는 0.6초 남짓이라 HUD 갱신 주기(0.1초)로 그리면 여섯 칸짜리 계단이 된다.
        // 문자열을 만들지 않는 스케일 대입 하나라 매 프레임 불러도 된다.
        if (hud != null && presenter != null) hud.SetCastProgress(presenter.CastProgress01);

        SyncLootPopup();
    }

    /// 개봉 게이지가 다 차면 창을 열고, 서버가 세션을 닫으면(이동·피격·밤 종료) 닫는다.
    /// F를 놓는 것으로는 닫히지 않는다 — 창은 캐스팅이 아니라 세션에 붙어 있다.
    ///
    /// 어떤 박스를 열었는지는 `PlayerInteract`가 이미 알고 있으므로 여기서 씬을 다시
    /// 뒤지지 않는다.
    void SyncLootPopup()
    {
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
