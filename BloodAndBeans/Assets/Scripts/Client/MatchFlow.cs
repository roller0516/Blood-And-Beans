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

    /// 루팅 창을 띄운 박스. 홀드가 끝나면 닫는다.
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

        presenter = new MatchHudPresenter(screen, phase, ledger, refreshInterval);
    }

    void Update()
    {
        presenter?.Tick(Time.unscaledTime);
        SyncLootPopup();
    }

    /// 박스 홀드를 시작하면 루팅 창을 열고, 놓거나 다른 박스로 옮기면 닫는다
    /// (기획서 7.2-2). 어떤 박스를 잡고 있는지는 `PlayerInteractor`가 이미 알고 있으므로
    /// 여기서 씬을 다시 뒤지지 않는다.
    void SyncLootPopup()
    {
        // 밤이 끝나면 서버가 홀드를 취소한다(`ItemBox.OnPhaseEntered`). 클라이언트가 F를
        // 붙잡고 있어도 담기는 더 이상 일어나지 않으므로 창도 같이 내린다.
        var night = phase != null && phase.IsSpawned && phase.Current == Phase.Night;
        var current = night ? presenter?.Interactor?.Current as ItemBox : null;
        var box = current != null ? current : null;   // 파괴된 박스는 없는 것으로 본다
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

        popup.Bind(box, presenter.BoxHold);
        lootOpen = true;
    }
}
