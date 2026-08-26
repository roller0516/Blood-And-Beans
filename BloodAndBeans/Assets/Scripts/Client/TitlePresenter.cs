using System;
using Unity.Netcode;

/// 타이틀 UI의 화면 흐름과 SteamLobby 연동을 담당한다.
/// View에는 표시할 값만 전달하고, 로비 상태 변경은 이 클래스 한 곳에서 구독한다.
///
/// MonoBehaviour가 아닌 이유는 이 클래스에 Unity 생명주기가 필요 없기 때문이다. 화면
/// 스택은 `UIManager`가, 로비 상태는 `SteamLobby`가 가진다. 여기 있는 것은 "지금 어느
/// 화면이고 무엇을 그려야 하는가"뿐이다.
public sealed class TitlePresenter
{
    readonly UIManager ui;
    readonly SteamLobby lobby;
    readonly string gameTitle;
    readonly Action onLocalClientConnected;
    readonly Action onQuit;

    int selectedRoom = NoRoom;
    bool active;
    bool subscribedToNetwork;

    const int NoRoom = -1;

    public TitlePresenter(UIManager ui, SteamLobby lobby, string gameTitle,
                          Action onLocalClientConnected, Action onQuit)
    {
        this.ui = ui;
        this.lobby = lobby;
        this.gameTitle = gameTitle;
        this.onLocalClientConnected = onLocalClientConnected;
        this.onQuit = onQuit;
    }

    public int TeamCount => lobby.TeamCount;

    public void Enable()
    {
        if (active) return;

        active = true;
        lobby.Changed += Render;
        SubscribeToNetwork();

        OpenScreen<TitleMenuScreen>();
    }

    public void Disable()
    {
        if (!active) return;

        active = false;
        lobby.Changed -= Render;

        var manager = NetworkManager.Singleton;
        if (manager != null && subscribedToNetwork)
            manager.OnClientConnectedCallback -= HideWhenLocalClientConnects;
        subscribedToNetwork = false;
    }

    void SubscribeToNetwork()
    {
        var manager = NetworkManager.Singleton;
        if (manager == null || subscribedToNetwork) return;

        manager.OnClientConnectedCallback += HideWhenLocalClientConnects;
        subscribedToNetwork = true;
    }

    void HideWhenLocalClientConnects(ulong clientId)
    {
        var manager = NetworkManager.Singleton;
        if (manager != null && clientId == manager.LocalClientId) onLocalClientConnected?.Invoke();
    }

    // --- 화면 이동 ---
    // 타이틀 → 방 목록 → 대기실은 되돌아갈 수 있는 흐름이라 스택으로 쌓는다.
    // "뒤로"와 "방 나가기"는 스택을 하나 내리는 것과 같다.

    public void OpenRooms()
    {
        OpenScreen<RoomListScreen>();
        RefreshRooms();
    }

    public void OpenSettings()
    {
        var popup = ui.PushPopup<SettingsPopup>();
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
    public async void CreateRoom()
    {
        if (await lobby.CreateRoomAsync(lobby.SuggestedRoomName) && active) EnterRoom();
    }

    public async void JoinRoom()
    {
        if (selectedRoom < 0 || selectedRoom >= lobby.Rooms.Count) return;

        var room = lobby.Rooms[selectedRoom];
        if (await lobby.JoinRoomAsync(room) && active) EnterRoom();
    }

    void EnterRoom()
    {
        var screen = OpenScreen<RoomScreen>();
        screen?.BuildTeams(lobby.TeamCount);
        Render();
    }

    public void SelectTeam(int team) => lobby.SelectTeam(team);

    public void StartMatch() => lobby.StartMatch();

    public void LeaveRoom()
    {
        lobby.LeaveRoom();
        ui.PopScreen();
        RefreshRooms();
    }

    /// 종료는 화면이 아니라 애플리케이션의 일이라 바깥에서 받는다.
    public void Quit() => onQuit?.Invoke();

    // --- 그리기 ---

    T OpenScreen<T>() where T : UIScreen
    {
        var screen = ui.PushScreen<T>();
        BindAll();
        return screen;
    }

    /// 화면은 재사용되므로 열릴 때마다 다시 건다. `UIButtons.Wire`가 먼저 지우고 걸어서
    /// 두 번 걸리지 않는다.
    void BindAll()
    {
        switch (ui.CurrentScreen)
        {
            case TitleMenuScreen menu:
                menu.Bind(gameTitle, OpenRooms, OpenSettings, Quit);
                break;
            case RoomListScreen rooms:
                rooms.Bind(RefreshRooms, CreateRoom, JoinRoom, BackToTitle, SelectRoom);
                break;
            case RoomScreen room:
                room.Bind(StartMatch, LeaveRoom, SelectTeam);
                break;
        }
    }

    void Render()
    {
        if (!active) return;

        switch (ui.CurrentScreen)
        {
            case RoomListScreen rooms:
                rooms.Render(lobby.Status, lobby.Rooms, selectedRoom);
                break;

            case RoomScreen room:
                var status = string.IsNullOrEmpty(lobby.Status)
                    ? $"{lobby.Members.Count}/{lobby.RoomCapacity}명 · 팀당 {lobby.PlayersPerTeam}명"
                    : lobby.Status;
                room.Render(lobby.RoomName, status, lobby.Members, lobby.SelectedTeam,
                            lobby.PlayersPerTeam, lobby.IsRoomHost, lobby.CanStartMatch,
                            lobby.OccupancyOf, lobby.TeamHasRoom);
                break;
        }
    }
}
