using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

/// 재생 전 준비. 팀 구성을 정해 두고 한 번에 매치까지 들어간다.
///
/// 시작 요청은 도메인 리로드를 건너 살아남아야 한다. 재생 진입이 곧 리로드라 필드에 담으면
/// 버튼을 누른 사실 자체가 사라진다. `SessionState`는 에디터를 닫을 때까지만 남으므로
/// 프로젝트 파일을 건드리지 않는다.
public class BootGroup : DevConsoleGroup
{
    public override string Tab => "시작";
    public override string Title => "검증 시작";

    /// 영속 오브젝트(NetworkManager · SteamLobby · MatchSeating)가 놓인 부팅 씬. 어느 씬에서
    /// 재생을 눌러도 여기를 먼저 거쳐야 한다. 매치 씬만 단독으로 열면 이들이 없어서 접속도
    /// 좌석 배정도 시작되지 않는다.
    const string BootScenePath = "Assets/Scenes/Launcher.unity";

    const string TeamsKey = "BB.DevConsole.BootTeams";
    const string MyTeamKey = "BB.DevConsole.BootMyTeam";
    const string StageKey = "BB.DevConsole.BootStage";

    const int StageIdle = 0;
    const int StageStartHost = 1;
    const int StageAwaitSeat = 2;

    IntegerField teamsField, myTeamField;
    Label sceneValue;
    Button startButton, clearButton;

    protected override void Build(VisualElement group)
    {
        var hint = new Label("팀을 정하고 검증 시작을 누르면 부팅 씬을 거쳐 호스트로 매치까지 들어간다.");
        hint.AddToClassList("hint");
        group.Add(hint);

        teamsField = FieldRow(group, "팀 수", 2);
        myTeamField = FieldRow(group, "내 팀", 0);
        startButton = Btn(ButtonRow(group), "검증 시작 (Host)", StartVerification, "btn--primary");

        sceneValue = Row(group, "부팅 씬", "-");
        clearButton = Btn(ButtonRow(group), "부팅 씬 해제", ClearBootScene);
    }

    public override void Refresh(in DevConsoleState state)
    {
        // 재생 중이면 이어받을 단계가 있는지 본다. 갱신 주기(약 10Hz)로 충분하다 —
        // 런처가 깨어나기를 기다리는 일이라 프레임 단위 정확도가 필요 없다.
        if (state.Playing) ApplyPendingBoot(state.Seating);
        // 매치까지 못 갔더라도 다음 재생에 되살아나면 안 된다. 다만 "재생 중이 아니다"로
        // 지우면 안 된다 — `EnterPlaymode`는 프레임 끝으로 미뤄지므로, 버튼을 누르고 재생이
        // 실제로 시작되기까지의 갱신 한 번이 방금 적은 요청을 지워 버린다.
        else if (!EditorApplication.isPlayingOrWillChangePlaymode) CancelPendingBoot();

        // 부팅 씬이 걸려 있으면 에디터의 재생 버튼도 그리로 간다. Inspector 어디에도 안 보이는
        // 설정이라, 어느 씬에서 눌러도 런처를 거치는 이유를 여기서 알 수 있게 적어 둔다.
        var start = EditorSceneManager.playModeStartScene;
        sceneValue.text = start == null ? "없음 (열린 씬에서 바로 재생)" : start.name;
        clearButton.SetEnabled(start != null);

        // 이미 재생 중이면 다시 시작할 수 없다.
        startButton.SetEnabled(!state.Playing);
        teamsField.SetEnabled(!state.Playing);
        myTeamField.SetEnabled(!state.Playing);
    }

    /// 부팅 씬을 걸고 재생에 들어간다. 실제 호스트 기동은 `ApplyPendingBoot`가 이어받는다 —
    /// 여기서 바로 부르면 뒤따르는 도메인 리로드에 씻겨 나간다.
    void StartVerification()
    {
        var boot = AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath);
        if (boot == null)
        {
            Debug.LogError($"{BootScenePath}를 찾을 수 없다. 부팅 씬 없이는 매치를 시작할 수 없다.");
            return;
        }

        SessionState.SetInt(TeamsKey, Mathf.Max(1, teamsField.value));
        SessionState.SetInt(MyTeamKey, Mathf.Max(0, myTeamField.value));
        SessionState.SetInt(StageKey, StageStartHost);

        EditorSceneManager.playModeStartScene = boot;
        if (!EditorApplication.isPlaying) EditorApplication.EnterPlaymode();
    }

    static void ClearBootScene() => EditorSceneManager.playModeStartScene = null;

    /// 재생이 시작된 뒤 런처의 영속 오브젝트가 깨어나기를 기다렸다가 팀을 앉히고 호스트로 뜬다.
    /// 매치 씬 로드는 `SteamLobby`가 서버 기동 이벤트에서 이미 걸어 둔다.
    static void ApplyPendingBoot(MatchSeating seating)
    {
        var stage = SessionState.GetInt(StageKey, StageIdle);
        if (stage == StageIdle) return;

        var manager = NetworkManager.Singleton;
        if (manager == null || seating == null) return;     // 런처가 아직 안 깨어났다

        if (stage == StageStartHost)
        {
            if (manager.IsListening) { CancelPendingBoot(); return; }    // 누가 이미 띄웠다

            seating.SetTeamCountCheat(SessionState.GetInt(TeamsKey, 1));
            seating.SetForcedSeatCheat(SessionState.GetInt(MyTeamKey, 0));

            if (!manager.StartHost())
            {
                Debug.LogError("검증 시작: 호스트로 뜨지 못했다. 전송 설정을 확인해라.");
                CancelPendingBoot();
                return;
            }

            SessionState.SetInt(StageKey, StageAwaitSeat);
            return;
        }

        // 호스트가 자기 자리를 받은 뒤에 강제 좌석을 푼다. 그대로 두면 뒤이어 붙는 MPPM 가상
        // 플레이어까지 같은 팀에 앉아 팀 격리를 검증할 수 없게 된다.
        if (PlayerTeam.Local() < 0) return;

        seating.SetForcedSeatCheat(MatchSeating.NoForcedSeat);
        CancelPendingBoot();
    }

    static void CancelPendingBoot()
    {
        if (SessionState.GetInt(StageKey, StageIdle) == StageIdle) return;
        SessionState.EraseInt(StageKey);
        SessionState.EraseInt(TeamsKey);
        SessionState.EraseInt(MyTeamKey);
    }
}
