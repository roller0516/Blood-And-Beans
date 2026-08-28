using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// 메인 에디터는 Host로, Multiplayer Play Mode 가상 플레이어는 Client로 시작시킨다.
/// 클릭 없이 2인 세션이 뜨게 하기 위한 것이다.
public class NetworkAutoStart : MonoBehaviour
{
    /// 어느 씬에서 시작하든 자동으로 뜬다. 로비를 거치는 정식 흐름을 막으므로 기본은 꺼짐이다.
    [SerializeField] bool enableAutoStart;

    /// 전투 씬 이름의 출처. 씬 이름을 여기 따로 적으면 `SteamLobby`와 갈라진다.
    [SerializeField] SteamLobby lobby;

    void Start()
    {
        if (enableAutoStart || StartedInGameScene()) StartNow();
    }

    /// 전투 씬을 열어 둔 채 재생했다. 로비를 거칠 방법이 없으므로 바로 붙는다.
    bool StartedInGameScene() =>
        lobby != null && SceneManager.GetActiveScene().name == lobby.GameScene;

    /// 지금 접속을 시작한다. 이미 떠 있으면 아무 일도 하지 않는다.
    public void StartNow()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.IsListening) return;

        if (IsMainEditor()) nm.StartHost();
        else nm.StartClient();
    }

    /// MPPM 가상 플레이어가 아닌가. MPPM은 에디터 전용이라 빌드에는 이 개념 자체가 없다.
    static bool IsMainEditor()
    {
#if UNITY_EDITOR
        return Unity.Multiplayer.PlayMode.CurrentPlayer.IsMainEditor;
#else
        return true;
#endif
    }
}
