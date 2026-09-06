using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

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
        lobby.MatchStarting += OnMatchStarting;
        SubscribeToNetwork();

        OpenScreen<UITitleMenuScreen>();
    }

    public void Disable()
    {
        if (!active) return;

        active = false;
        lobby.Changed -= Render;
        lobby.MatchStarting -= OnMatchStarting;

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
    public async void CreateRoom(int teamCount)
    {
        if (await lobby.CreateRoomAsync(lobby.SuggestedRoomName, teamCount) && active) EnterRoom();
    }

    public async void JoinRoom()
    {
        if (selectedRoom < 0 || selectedRoom >= lobby.Rooms.Count) return;

        var room = lobby.Rooms[selectedRoom];
        if (await lobby.JoinRoomAsync(room) && active) EnterRoom();
    }

    /// 방에 들어가면 방 화면이다. 캐릭터 선택은 매치가 열릴 때 온다 (기획서 10.1의
    /// 표 순서: 방 → 캐릭터 선택 → 매치).
    void EnterRoom() => OpenRoom();

    /// 방장이 다른 곳에서 시작을 눌렀다. 손님도 같은 자리에서 캐릭터를 고른다.
    void OnMatchStarting()
    {
        if (!active) return;
        OpenCharacterSelect();
    }

    void OpenCharacterSelect()
    {
        var screen = OpenScreen<UICharacterSelectScreen>();

        // 프리팹을 이어 두지 않았으면 선택을 건너뛰고 그대로 매치로 간다. 여기서 멈추면
        // 시작 자체가 불가능해진다 — 기획서 9.3도 고르지 않은 채 시작하는 것을 허용한다.
        if (screen == null) { ConfirmCharacter(); return; }
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

    /// 「확정」. 픽은 이미 로비에 들어가 있고(`SteamLobby.SelectCharacter`), 여기서
    /// 실제로 매치가 열린다 — 방장은 서버를 띄우고 손님은 거기 붙는다.
    ///
    /// 접속보다 픽이 먼저여야 한다. `PlayerCharacter.OnNetworkSpawn`이
    /// `GameManager.SelectedCharacter`를 읽어 서버로 올리기 때문이다.
    void ConfirmCharacter()
    {
        if (lobby.IsRoomHost) lobby.StartMatch();
        else lobby.JoinStartedMatch();
    }

    /// 캐릭터 선택의 「뒤로」. 방으로 돌아간다.
    ///
    /// ponytail: 손님이 여기서 물러나면 이미 열린 매치에 붙을 길이 사라진다. 방장을
    /// 기다리게 하지 않으려고 재입장 경로를 만들지 않았다 — 필요해지면 방 화면에
    /// 「합류」를 단다.
    void CancelCharacterSelect()
    {
        ui.PopScreen();
        Render();
    }

    public void SelectTeam(int team) => lobby.SelectTeam(team);

    /// 방장의 「게임 시작」. 바로 뜨지 않고 캐릭터 선택을 먼저 연다 (기획서 10.1).
    /// 실제 시작은 「확정」이 한다 (`ConfirmCharacter`).
    public void StartMatch() => OpenCharacterSelect();

    public void LeaveRoom()
    {
        lobby.LeaveRoom();

        // 캐릭터 선택이 열려 있으면 방 화면 위에 있다. 둘 다 걷어야 방 목록이 나온다.
        if (ui.CurrentScreen is UICharacterSelectScreen) ui.PopScreen();
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
            case UITitleMenuScreen menu:
                menu.Bind(gameTitle, OpenRooms, OpenSettings, Quit);
                break;
            case UIRoomListScreen rooms:
                rooms.Bind(RefreshRooms, CreateRoom, JoinRoom, BackToTitle, SelectRoom, lobby.MaxTeams);
                break;
            case UIRoomScreen room:
                room.Bind(StartMatch, LeaveRoom, SelectTeam, lobby.ToggleReady);
                break;

            case UICharacterSelectScreen pick:
                // 같은 팀이 집어 간 칸을 보여 준다 (기획서 3.4: 팀원끼리는 서로의 픽이
                // 보여야 9.1의 중복 픽 금지가 성립한다). 최종 판정은 서버 한 곳이다
                // (`PlayerCharacter.PickRpc`, 기획서 9.3) — 이건 표시일 뿐이다.
                pick.Bind(
                    TeamClaims(),
                    lobby.SelectedCharacter,
                    0,
                    lobby.SuggestedRoomName,
                    "NIGHT ACTIVE",
                    "밤 액티브는 키보드 1로 쓴다 (기획서 9.2)",
                    SelectCharacter, SelectCharacterColor, ConfirmCharacter, CancelCharacterSelect);
                break;
        }
    }

    /// 같은 팀이 이미 집어 간 칸. 매 갱신마다 리스트를 새로 만들지 않도록 재사용한다.
    readonly List<UICharacterSelectScreen.Claim> teamClaims = new();

    IReadOnlyList<UICharacterSelectScreen.Claim> TeamClaims()
    {
        teamClaims.Clear();
        foreach (var mate in lobby.TeammatesWithPick(lobby.SelectedTeam))
            teamClaims.Add(new UICharacterSelectScreen.Claim(
                mate.Character, mate.Name, ClaimColor));
        return teamClaims;
    }

    /// 남이 집어 간 칸의 표시색. 기획서에 규칙이 없어 화면 안에서만 쓴다.
    static readonly Color ClaimColor = new(0.55f, 0.58f, 0.62f, 1f);

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
                            lobby.OccupancyOf, lobby.TeamHasRoom,
                            lobby.SelfReady, lobby.ReadyCount);
                break;
        }
    }
}
