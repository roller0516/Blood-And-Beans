using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

/// 게임의 진입 씬. 부팅이 끝나기를 기다렸다가 타이틀 씬으로 넘긴다.
///
/// 예전에는 이 씬이 영속 오브젝트를 들고 `DontDestroyOnLoad`로 넘기는 일까지 했다. 그래서
/// 이 씬을 거치지 않고 시작하면 그것들이 통째로 없었다. 지금은 `GameManager`가 어느 씬에서
/// 시작하든 만들고 부팅 순서까지 쥐므로, 여기 남은 일은 "준비되면 타이틀로"뿐이다.
public class Launcher : MonoBehaviour
{
    /// 부팅이 끝나면 갈 곳. Build Settings에 들어 있어야 한다.
    [SerializeField] string titleScene = "Title";

    /// Unity 생명주기 진입점이다. `UniTaskVoid`라 오브젝트가 파괴되면 취소가 조용히 걷힌다.
    async UniTaskVoid Start()
    {
        var token = this.GetCancellationTokenOnDestroy();

        // 스팀까지 열린 뒤에 타이틀을 띄운다. 먼저 띄우면 방 목록이 "스팀이 준비되지 않았다"를
        // 한 번 그리고, 그 화면은 스팀이 열려도 스스로 다시 그리지 않는다.
        await UniTask.WaitUntil(() => GameManager.Instance != null && GameManager.Instance.IsReady,
                                cancellationToken: token);

        await SceneManager.LoadSceneAsync(titleScene).ToUniTask(cancellationToken: token);
    }
}
