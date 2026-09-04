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

        OpenScreen<UITitleMenuScreen>();
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
        OpenScreen<UIRoomListScreen>();
        RefreshRooms();
    }

    public void OpenSettings()
    {
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

    /// 방에 들어가면 캐릭터를 먼저 고른다 (기획서 9장 · 목업 2번).
    ///
    /// 방 화면보다 앞에 두는 이유는 「확정」이 스택을 하나 쌓는 흐름과 맞기 때문이다 —
    /// 뒤로 가면 방 목록으로 돌아가고, 확정하면 방 화면이 그 위에 올라간다. 방 화면에
    /// 버튼을 새로 다는 것보다 흐름이 짧다.
    void EnterRoom() => OpenCharacterSelect();

    void OpenCharacterSelect()
    {
        var screen = OpenScreen<UICharacterSelectScreen>();

        // 프리팹을 이어 두지 않았으면 캐릭터 없이 방으로 보낸다. 여기서 멈추면 방에
        // 들어갈 방법 자체가 사라진다.
        if (screen == null) { OpenRoom(); return; }
        Render();
    }

    void OpenRoom()
    {
        var screen = OpenScreen<UIRoomScreen>();
        screen?.BuildTeams(lobby.TeamCount);
        Render();
    }

    /// 카드를 눌렀다. 확정 전까지는 로비에만 보관한다 (`SteamLobby.SelectCharacter`).
    public void SelectCharacter(int index) => lobby.SelectCharacter(index);

    /// 팀 색은 아직 로비 상태가 아니다.
    /// ponytail: 목업 2번의 네임플레이트 색 선택은 기획서에 규칙이 없다. 화면 안에서만
    /// 유지되고 서버로 가지 않는다. 기획서에 색 규칙이 생기면 로비 멤버 데이터로 옮긴다.
    public void SelectCharacterColor(int index) { }

    /// 「확정」. 픽은 이미 로비에 들어가 있으므로 화면만 넘긴다.
    void ConfirmCharacter() => OpenRoom();

    public void SelectTeam(int team) => lobby.SelectTeam(team);

    public void StartMatch() => lobby.StartMatch();

    public void LeaveRoom()
    {
        lobby.LeaveRoom();

        // 방에 들어가면 화면이 둘 쌓인다 (캐릭터 선택 → 방). 방 화면에서 나갈 때는
        // 그 아래 캐릭터 선택까지 함께 걷어야 방 목록이 나온다.
        ui.PopScreen();
        if (ui.CurrentScreen is UICharacterSelectScreen) ui.PopScreen();

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
            case UITitleMenuScreen menu:
                menu.Bind(gameTitle, OpenRooms, OpenSettings, Quit);
                break;
            case UIRoomListScreen rooms:
                rooms.Bind(RefreshRooms, CreateRoom, JoinRoom, BackToTitle, SelectRoom);
                break;
            case UIRoomScreen room:
                room.Bind(StartMatch, LeaveRoom, SelectTeam);
                break;

            case UICharacterSelectScreen pick:
                // 남이 집어 간 칸은 아직 표시하지 않는다.
                // ponytail: 로비 멤버 데이터에 픽이 실리기 전까지 팀 내 중복 픽 금지
                // (기획서 9.1)는 서버 `PlayerCharacter.PickRpc`만 판정한다. 화면은
                // 거절된 픽을 되돌리지 못하고 그대로 둔다.
                pick.Bind(
                    System.Array.Empty<UICharacterSelectScreen.Claim>(),
                    lobby.SelectedCharacter,
                    0,
                    lobby.SuggestedRoomName,
                    "NIGHT ACTIVE",
                    "밤 액티브는 키보드 1로 쓴다 (기획서 9.2)",
                    SelectCharacter, SelectCharacterColor, ConfirmCharacter, LeaveRoom);
                break;
        }
    }

    void Render()
    {
        if (!active) return;

        switch (ui.CurrentScreen)
        {
            case UIRoomListScreen rooms:
                rooms.Render(lobby.Status, lobby.Rooms, selectedRoom);
                break;

            case UIRoomScreen room:
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
