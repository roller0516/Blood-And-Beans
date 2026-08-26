using UnityEngine;

/// 타이틀 씬의 조립 지점. UIManager와 SteamLobby를 이어 Presenter를 만든다.
///
/// 이 클래스가 따로 있는 이유는 Presenter가 화면보다 오래 살아야 하기 때문이다. 예전에는
/// 화면 클래스(`TitleScreen`)가 자기 Presenter를 만들었는데, 화면을 셋으로 쪼개면서
/// "누가 Presenter를 소유하는가"에 답이 없어졌다. 씬 오브젝트인 여기가 소유한다.
public sealed class TitleFlow : MonoBehaviour
{
    [SerializeField] UIManager ui;

    [Header("표시")]
    [SerializeField] string gameTitle = "Blood & Beans";

    TitlePresenter presenter;

    void Awake()
    {
        if (ui == null)
        {
            Debug.LogError($"{name}: UIManager가 연결되지 않았다. 화면을 열 수 없다.", this);
            enabled = false;
            return;
        }

        // 로비는 런처 씬에서 살아 넘어오는 영속 오브젝트라 Inspector로 이을 수 없다.
        // 씬 로드 시 조립 지점에서 한 번만 찾는다.
        var lobby = FindAnyObjectByType<SteamLobby>();
        if (lobby == null)
        {
            Debug.LogError($"{name}: {nameof(SteamLobby)}가 없다. 런처 씬을 거치지 않고 타이틀을 "
                         + "직접 실행했다는 뜻이다.", this);
            enabled = false;
            return;
        }

        presenter = new TitlePresenter(ui, lobby, gameTitle, HideAllUI, QuitApplication);
    }

    /// UIManager가 팀 수를 아는 시점(로비 준비 후)에 첫 화면을 연다.
    void Start() => presenter?.Enable();

    void OnDisable() => presenter?.Disable();

    /// 매치에 접속되면 로비 UI는 통째로 물러난다. 화면 하나하나를 내리는 대신 루트를
    /// 끄는 이유는, 돌아올 때 스택을 그대로 되살리기 위해서다.
    void HideAllUI()
    {
        if (ui != null) ui.gameObject.SetActive(false);
    }

    void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
