using System;
using Cysharp.Threading.Tasks;

/// 타이틀 씬의 화면 흐름(타이틀 메뉴 → 방 목록)과 SteamLobby 연동을 담당한다.
/// View에는 표시할 값만 전달하고, 로비 상태 변경은 이 클래스 한 곳에서 구독한다.
///
/// 방에 들어가면 캐릭터 선택 씬으로 넘어가고, 그 뒤는 `CharacterSelectPresenter`가 맡는다.
///
/// MonoBehaviour가 아닌 이유는 이 클래스에 Unity 생명주기가 필요 없기 때문이다. 화면
/// 스택은 `UIManager`가, 로비 상태는 `SteamLobby`가, 씬 전환은 `GameSceneManager`가 가진다.
/// 여기 있는 것은 "지금 어느 화면이고 무엇을 그려야 하는가"뿐이다.
public sealed class TitlePresenter
{
    readonly UIManager ui;
    readonly SteamLobby lobby;
    readonly GameSceneManager scenes;
    readonly string gameTitle;
    readonly Action onQuit;

    int selectedRoom = NoRoom;
    bool active;

    const int NoRoom = -1;

    public TitlePresenter(UIManager ui, SteamLobby lobby, GameSceneManager scenes, string gameTitle, Action onQuit)
    {
        this.ui = ui;
        this.lobby = lobby;
        this.scenes = scenes;
        this.gameTitle = gameTitle;
        this.onQuit = onQuit;
    }

    /// 첫 화면(타이틀 메뉴)을 연다. 화면이 다 뜨면 끝난다.
    public async UniTask EnterAsync()
    {
        if (active) return;

        active = true;
        lobby.Changed += Render;

        SoundManager.Instance.PlayBgm(Bgm.Title);
        await OpenScreenAsync<UITitleMenuScreen>();
    }

    public void Disable()
    {
        if (!active) return;

        active = false;
        lobby.Changed -= Render;
    }

    // --- 화면 이동 ---
    // 타이틀 → 방 목록은 되돌아갈 수 있는 흐름이라 스택으로 쌓는다.
    // "뒤로"는 스택을 하나 내리는 것과 같다.

    public void OpenRooms() => OpenRoomsAsync().Forget();

    async UniTaskVoid OpenRoomsAsync()
    {
        if (await OpenScreenAsync<UIRoomListScreen>() != null) RefreshRooms();
    }

    public void OpenSettings() => OpenSettingsAsync().Forget();

    async UniTaskVoid OpenSettingsAsync()
    {
        await ui.LoadAsync<UISettingsPopup>();
        if (!active) return;
        var popup = ui.PushPopup<UISettingsPopup>();
        popup?.Bind(ui.PopPopup);
    }

    public void BackToTitle()
    {
        ui.PopScreen();
        Render();
    }

    public void SelectRoom(int index)
    {
        if (index < 0 || index >= lobby.Rooms.Count) return;

        selectedRoom = index;
        Render();
    }

    /// uGUI 버튼 이벤트 진입점이므로 async void를 사용한다.
    public async void RefreshRooms()
    {
        selectedRoom = NoRoom;
        Render();
        await lobby.RefreshRoomsAsync();
    }

    /// 방 이름 입력 팝업이 생기기 전까지는 Steam 표시 이름을 재사용한다.
    public async void CreateRoom(int teamCount)
    {
        if (await lobby.CreateRoomAsync(lobby.SuggestedRoomName, teamCount) && active) scenes.GoToRoom();
    }

    public async void JoinRoom()
    {
        if (selectedRoom < 0 || selectedRoom >= lobby.Rooms.Count) return;

        var room = lobby.Rooms[selectedRoom];
        if (await lobby.JoinRoomAsync(room) && active) scenes.GoToRoom();
    }

    /// 종료는 화면이 아니라 애플리케이션의 일이라 바깥에서 받는다.
    public void Quit() => onQuit?.Invoke();

    // --- 그리기 ---

    /// 처음 여는 화면은 프리팹을 불러와야 한다. 기다리는 사이 타이틀을 떠났으면 열지 않는다.
    async UniTask<T> OpenScreenAsync<T>() where T : UIScreen
    {
        await ui.LoadAsync<T>();
        if (!active) return null;
        var screen = ui.PushScreen<T>();
        if (screen != null) BindAll();
        return screen;
    }

    /// 화면은 재사용되므로 열릴 때마다 다시 건다. `UIButtons.Wire`가 먼저 지우고 걸어서
    /// 두 번 걸리지 않는다.
    void BindAll()
    {
        switch (ui.CurrentScreen)
        {
            case UITitleMenuScreen menu:
                menu.Bind(gameTitle, OpenRooms, OpenSettings, Quit);
                break;
            case UIRoomListScreen rooms:
                rooms.Bind(RefreshRooms, CreateRoom, JoinRoom, BackToTitle, SelectRoom, lobby.MaxTeams);
                break;
        }
    }

    void Render()
    {
        if (!active) return;

        if (ui.CurrentScreen is UIRoomListScreen rooms)
            rooms.Render(lobby.Status, lobby.Rooms, selectedRoom);
    }
}
