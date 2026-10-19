using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;

/// 한 씬이 잡은 에셋 묶음. `GameSceneManager`가 씬마다 하나 만들어 `SceneFlow.PrepareAsync`에
/// 넘기고, 다음 씬이 자기 것을 다 잡은 뒤에 Dispose한다. 두 씬이 같이 쓰는 에셋은 그 사이 참조
/// 횟수가 0이 되지 않아 다시 불러오지 않는다 — 공유 판정은 각 창구의 횟수가 이미 한다.
///
/// 잡을 때 놓는 법을 먼저 적는다. 불러오는 도중에 취소돼도 횟수는 이미 셌으므로 놓아야 한다.
public sealed class AssetScope : IDisposable
{
    readonly List<Action> releases = new();

    public UniTask SpritesAsync(ResourceManager.SpriteTables tables, CancellationToken ct)
    {
        var resources = ResourceManager.Instance;
        releases.Add(() => resources.ReleaseSprites(tables));
        return resources.PreloadSpritesAsync(tables, ct);
    }

    /// 화면·팝업 프리팹. 이 묶음이 살아 있는 동안 `UIManager.UnloadUnused`가 내리지 않는다.
    public UniTask ViewAsync<T>(CancellationToken ct) where T : UIView
    {
        var ui = UIManager.Instance;
        ui.Pin<T>();
        releases.Add(ui.Unpin<T>);
        return ui.LoadAsync<T>(ct);
    }

    /// 매치 공통 연출. 놓을 때 이번 매치에서 불러온 캐릭터 전용 연출도 함께 놓는다 (`EffectManager.EndMatch`).
    public UniTask MatchEffectsAsync(CancellationToken ct)
    {
        releases.Add(EffectManager.EndMatch);
        return EffectManager.PrepareCommonAsync(ct);
    }

    public void Dispose()
    {
        for (var i = releases.Count - 1; i >= 0; i--) releases[i]();
        releases.Clear();
    }
}
