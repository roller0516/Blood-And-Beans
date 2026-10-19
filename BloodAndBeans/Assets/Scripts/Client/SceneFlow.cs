using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// 씬마다 하나 놓는 조립 지점. 시작하면 `GameSceneManager`에 도착을 알리고, 매니저가 로딩 창을
/// 붙잡은 채 `PrepareAsync`(에셋 잡기) → 앞 씬 에셋 정리 → `EnterAsync`(첫 화면) 순으로 부른다.
///
/// 에셋을 직접 잡고 놓지 않는다. 받은 `AssetScope`로 잡으면 놓는 것은 매니저가 한다.
public abstract class SceneFlow : MonoBehaviour
{
    /// Awake가 아니라 Start다. 같은 씬의 다른 오브젝트가 다 깨어난 뒤에 준비를 시작한다.
    protected virtual void Start()
    {
        var scenes = GameSceneManager.Instance;
        if (scenes == null)
        {
            CDebug.LogError($"{name}: {nameof(GameSceneManager)}가 없다. 이 씬을 준비할 수 없다.", this);
            return;
        }
        scenes.Arrive(this);
    }

    /// 이 씬이 쓸 에셋을 잡는다. 잡는 것만 하고 화면은 열지 않는다.
    public abstract UniTask PrepareAsync(AssetScope scope, CancellationToken ct);

    /// 에셋이 다 준비된 뒤 첫 화면을 연다. 끝나면 로딩 창이 내려간다.
    public abstract UniTask EnterAsync(CancellationToken ct);
}
