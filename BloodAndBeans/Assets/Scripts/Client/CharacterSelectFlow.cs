using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// 캐릭터 선택 씬의 조립 지점. 방에 들어가면 이 씬이다 (`GameSceneManager.GoToRoom`).
///
/// 무대(`CharacterStage`)는 여기서 세운다. 생성된 무대는 이 씬에 속해 씬과 함께 내려가고, 프리팹
/// 참조도 이 씬이 쥐어 씬과 함께 메모리에서 빠진다. 화면은 씬을 넘어 사는 UI라 무대를 직접 잡을 수
/// 없어서 여기서 넘긴다. 씬에 카메라를 따로 두지 않는다 — 두면 무대 카메라와 겹쳐 같은 화면을
/// 두 번 그린다.
///
/// 씬에 미리 놓지 않고 생성하는 이유: 무대의 카페 소품(`Counter`·`Shared*`)에 `NetworkObject`가
/// 있다. 씬에 놓으면 NGO가 씬 배치 오브젝트로 보고 호스트 시작 때 스폰해 설비 로직이 돈다.
/// 실행 중에 만든 것은 스폰하지 않으므로 장식으로만 남는다.
public sealed class CharacterSelectFlow : SceneFlow
{
    [SerializeField] CharacterStage stagePrefab;

    CharacterSelectPresenter presenter;

    void Awake()
    {
        if (stagePrefab == null) CDebug.LogError($"{name}: {nameof(stagePrefab)}가 비어 있다. 무대 없이 화면만 뜬다.", this);
    }

    /// 카드 초상이 캐릭터 스프라이트 표를 꺼낸다. 카드는 화면을 열 때(`Bind`) 찍으므로 둘을 함께 불러도 된다.
    public override UniTask PrepareAsync(AssetScope scope, CancellationToken ct) =>
        UniTask.WhenAll(
            scope.SpritesAsync(ResourceManager.SpriteTables.Crews, ct),
            scope.ViewAsync<UICharacterSelectScreen>(ct));

    public override UniTask EnterAsync(CancellationToken ct)
    {
        var stage = stagePrefab != null ? Instantiate(stagePrefab) : null;
        presenter = new CharacterSelectPresenter(UIManager.Instance, GameManager.Lobby, GameSceneManager.Instance, stage);
        return presenter.EnterAsync();
    }

    void OnDisable() => presenter?.Disable();
}
