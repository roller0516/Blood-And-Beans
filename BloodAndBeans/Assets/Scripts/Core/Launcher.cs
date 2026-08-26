using UnityEngine;
using UnityEngine.SceneManagement;

/// 게임의 진입 씬. 이 씬에 놓인 것은 전부 씬을 넘어 살아남고, 그다음 타이틀 씬으로 넘긴다.
///
/// 런처가 따로 있는 이유는 수명이다. 스팀 세션(`SteamLobby`)과 접속 승인(`MatchSeating`)은
/// 타이틀보다 오래, 게임 씬보다도 오래 살아야 한다 — 클라이언트는 게임 씬이 로드되기 전에
/// 접속하고, 매치가 끝난 뒤에도 방 목록을 다시 받아야 한다. 타이틀 씬에 두면 게임 씬을
/// 로드하는 순간 같이 죽는다.
///
/// 무엇을 살릴지 Inspector로 고르게 하지 않는다. "런처 씬에 있으면 영속"이 규칙이고,
/// 목록을 따로 두면 오브젝트를 추가하고 목록에 넣는 것을 잊는 순간 조용히 사라진다.
public class Launcher : MonoBehaviour
{
    /// 부팅이 끝나면 갈 곳. Build Settings에 들어 있어야 한다.
    [SerializeField] string titleScene = "Title";

    void Start()
    {
        // 자기 자신은 남길 이유가 없다. 런처의 일은 여기서 끝난다.
        foreach (var root in gameObject.scene.GetRootGameObjects())
            if (root != gameObject) DontDestroyOnLoad(root);
        
        SceneManager.LoadScene(titleScene);
    }
}
