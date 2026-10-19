using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// 타이틀 씬의 조립 지점. 에셋 준비와 정리는 `GameSceneManager`가 하고, 여기는 무엇을 잡을지와
/// 첫 화면을 여는 Presenter를 만드는 일만 한다.
///
/// Presenter를 이 씬 오브젝트가 소유하는 이유는 화면보다 오래 살아야 하기 때문이다. 화면은
/// 타이틀 메뉴와 방 목록 둘이고, 씬은 그 둘을 다 지난다.
public sealed class TitleFlow : SceneFlow
{
    [Header("표시")]
    [SerializeField] string gameTitle = "Blood & Beans";

    TitlePresenter presenter;

    public override UniTask PrepareAsync(AssetScope scope, CancellationToken ct) =>
        scope.ViewAsync<UITitleMenuScreen>(ct);

    public override UniTask EnterAsync(CancellationToken ct)
    {
        presenter = new TitlePresenter(UIManager.Instance, GameManager.Lobby, GameSceneManager.Instance,
                                       gameTitle, QuitApplication);
        return presenter.EnterAsync();
    }

    void OnDisable() => presenter?.Disable();

    void QuitApplication()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
