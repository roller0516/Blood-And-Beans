using System;
using Cysharp.Threading.Tasks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// 씬 전환의 단일 창구. 어느 씬으로 언제 넘어가는지, 넘어가는 동안의 로딩 창, 씬이 쥔 에셋의
/// 정리를 맡는다. `GameManager` 프리팹의 자식이라 씬을 넘어 산다 (`UIManager`와 같은 이유).
///
/// 새 씬의 `SceneFlow`가 자기 에셋을 다 잡은 **뒤에** 앞 씬의 `AssetScope`를 놓는다. 두 씬이
/// 같이 쓰는 에셋은 참조 횟수가 0이 되지 않아 다시 불러오지 않는다.
///
/// 전투 씬은 서버가 불러 준다(NGO). 여기서는 붙은 순간 로딩 창을 띄우고, 전투 씬의 `MatchFlow`가
/// 도착하면 이어받는다.
// ponytail: 전환하는 동안 두 씬 몫의 에셋이 잠깐 함께 올라간다. 프로파일로 확인하지 않았다 —
// 정점이 문제가 되면 앞 씬을 먼저 놓고 공유 에셋만 남기는 비교를 ArriveAsync에 넣는다.
public sealed class GameSceneManager : MonoSingleton<GameSceneManager>
{
    [Header("씬")]
    /// 방 밖에 있을 때의 씬 (타이틀·방 목록). Build Settings에 있어야 한다.
    [SerializeField] string titleScene = "Title";

    /// 방 안에 있을 때의 씬 (캐릭터 선택·준비).
    [SerializeField] string roomScene = "CharacterSelect";

    /// 방·화면 스택. 둘 다 씬을 넘어 사는 형제라 깨어날 때 한 번 받아 둔다.
    SteamLobby lobby;
    UIManager ui;

    /// 지금 씬이 잡은 에셋. 다음 씬이 자기 것을 다 잡은 뒤에 놓는다.
    AssetScope current;

    /// 씬을 넘기기로 한 순간부터 새 씬이 도착할 때까지 붙잡는 로딩 창.
    UIManager.LoadingHandle pending;

    protected override void Awake()
    {
        base.Awake();
        if (Instance != this) return;

        lobby = GameManager.Lobby;
        ui = UIManager.Instance;
        if (lobby == null || ui == null)
        {
            CDebug.LogError($"{name}: {nameof(lobby)} 또는 {nameof(ui)}가 비어 있다. 씬을 넘길 수 없다.", this);
            enabled = false;
            return;
        }

        lobby.MatchLeft += OnMatchLeft;

        // 접속 계층은 게임매니저보다 먼저 선다 (`GameManager.CreateIfMissing`).
        var network = NetworkManager.Singleton;
        if (network != null) network.OnClientConnectedCallback += OnClientConnected;
    }

    void OnDestroy()
    {
        if (lobby != null) lobby.MatchLeft -= OnMatchLeft;
        var network = NetworkManager.Singleton;
        if (network != null) network.OnClientConnectedCallback -= OnClientConnected;

        pending?.Dispose();
        current?.Dispose();
    }

    public void GoToTitle() => GoTo(titleScene);
    public void GoToRoom() => GoTo(roomScene);

    void GoTo(string scene)
    {
        BeginTransition();
        if (SceneManager.LoadSceneAsync(scene) != null) return;

        // 빌드 목록에 없는 씬이다. Unity가 이미 오류를 남겼다 — 로딩 창만 거둔다.
        pending.Dispose();
        pending = null;
    }

    /// 씬이 곧 바뀐다. 로딩 창을 띄우고 이 씬의 화면을 내린다. 새 씬이 도착하면 놓는다.
    void BeginTransition()
    {
        pending ??= ui.BeginLoading();
        ui.PopAllPopups();
        ui.ClearScreens();
    }

    /// 매치에 붙었다. 서버가 전투 씬을 불러 줄 것이므로 그때까지 덮는다. 전투 씬에서 바로
    /// 재생해 붙었으면 이미 그 씬이라 덮을 것이 없다.
    void OnClientConnected(ulong clientId)
    {
        var network = NetworkManager.Singleton;
        if (network == null || clientId != network.LocalClientId) return;
        if (lobby.IsGameScene(SceneManager.GetActiveScene().name)) return;
        BeginTransition();
    }

    /// 매치 연결이 끊겼다. 방에 남아 있으면 캐릭터 선택으로, 아니면 타이틀로 돌아간다.
    void OnMatchLeft(bool inRoom)
    {
        var scene = inRoom ? roomScene : titleScene;
        // 이미 그 씬이고 넘어가는 중도 아니면 화면이 살아 있다. 다시 부르지 않는다.
        if (pending == null && SceneManager.GetActiveScene().name == scene) return;
        GoTo(scene);
    }

    /// 씬의 `SceneFlow`가 시작했다. 로딩 창을 붙잡은 채 그 씬의 에셋을 잡고, 앞 씬 것을 놓고,
    /// 첫 화면을 연다.
    public void Arrive(SceneFlow flow) => ArriveAsync(flow).Forget();

    async UniTaskVoid ArriveAsync(SceneFlow flow)
    {
        // 넘기던 로딩 창을 이어받는다. 씬에서 바로 재생했으면 넘겨받을 것이 없어 새로 잡는다.
        using var loading = pending ?? ui.BeginLoading();
        pending = null;
        ui.PopAllPopups();
        ui.ClearScreens();

        var token = flow.GetCancellationTokenOnDestroy();
        var next = new AssetScope();
        try
        {
            await flow.PrepareAsync(next, token);
        }
        catch
        {
            // 준비 중에 씬이 또 바뀌었거나 실패했다. 잡은 것만 놓는다 — 앞 씬 것은 다음 도착이 놓는다.
            next.Dispose();
            throw;
        }

        current?.Dispose();
        current = next;

        // 앞 씬이 지나가며 연 팝업·화면 중 어느 씬도 붙잡지 않은 것을 내리고, 로딩 창 뒤에서
        // 한 번 비운다. 끊김이 로딩 창에 가려진다.
        ui.UnloadUnused();
        await Resources.UnloadUnusedAssets().ToUniTask(cancellationToken: token);
        GC.Collect();

        await flow.EnterAsync(token);
    }
}
