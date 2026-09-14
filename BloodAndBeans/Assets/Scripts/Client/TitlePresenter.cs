using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
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

        if (lobby.InRoom) EnterRoom();
        else OpenScreenAsync<UITitleMenuScreen>().Forget();
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
        if (await lobby.CreateRoomAsync(lobby.SuggestedRoomName, teamCount) && active) EnterRoom();
    }

    public async void JoinRoom()
    {
        if (selectedRoom < 0 || selectedRoom >= lobby.Rooms.Count) return;

        var room = lobby.Rooms[selectedRoom];
        if (await lobby.JoinRoomAsync(room) && active) EnterRoom();
    }

    /// 방에 들어가면 곧바로 캐릭터 선택 화면이다. 대기실을 따로 거치지 않고 픽·준비·시작을
    /// 한 화면에서 본다.
    ///
    /// 화면 프리팹을 이어 두지 않았으면 예전 대기실로 물러난다 — 여기서 멈추면 방에
    /// 들어가고도 아무것도 못 한다.
    void EnterRoom() => EnterRoomAsync().Forget();

    async UniTaskVoid EnterRoomAsync()
    {
        if (await OpenScreenAsync<UICharacterSelectScreen>() == null)
        {
            if (active) OpenRoomAsync().Forget();
            return;
        }
        Render();
    }

    /// 방장이 매치를 열었다. 손님은 이미 캐릭터를 고른 상태이므로 바로 붙는다.
    void OnMatchStarting()
    {
        if (!active) return;
        lobby.JoinStartedMatch();
    }

    async UniTaskVoid OpenRoomAsync()
    {
        var screen = await OpenScreenAsync<UIRoomScreen>();
        if (screen == null) return;
        screen.BuildTeams(lobby.TeamCount);
        Render();
    }

    /// 카드를 눌렀다. 픽은 스팀 로비 멤버 데이터로 바로 나가 같은 방의 모두가 본다
    /// (`SteamLobby.SelectCharacter`).
    public void SelectCharacter(int index) => lobby.SelectCharacter(index);

    /// 캐릭터 선택 화면 바닥의 큰 버튼. 방장에게는 「게임 시작」이고 나머지에게는
    /// 「준비」다 (기획서 10.1). 글자와 잠금은 `UICharacterSelectScreen.SetLobby`가 그린다.
    ///
    /// 접속보다 픽이 먼저여야 한다. `PlayerCharacter.OnNetworkSpawn`이
    /// `GameManager.SelectedCharacter`를 읽어 서버로 올리기 때문이다 — 픽은 이미
    /// 로비에 들어가 있으므로 여기서는 순서를 걱정하지 않는다.
    void StartOrReady()
    {
        if (lobby.IsRoomHost) lobby.StartMatch();
        else lobby.ToggleReady();
    }

    public void SelectTeam(int team) => lobby.SelectTeam(team);

    /// 방장의 「게임 시작」.
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
            case UIRoomScreen room:
                room.Bind(StartMatch, LeaveRoom, SelectTeam, lobby.ToggleReady);
                break;

            case UICharacterSelectScreen pick:
                // 방의 누가 무엇을 골랐는지 보여 준다 (기획서 3.4). 최종 판정은 서버
                // 한 곳이다 (`PlayerCharacter.PickRpc`, 기획서 9.3) — 이건 표시일 뿐이다.
                // 다섯째 인자는 팀이다. 캐릭터 선택창의 팀 칸이 곧 팀 선택이라 로비의
                // 현재 팀으로 열고, 누르면 `SelectTeam`이 로비 멤버 데이터로 써서
                // 같은 방의 모두가 즉시 본다.
                pick.Bind(
                    Claims(),
                    lobby.SelectedCharacter,
                    lobby.SelectedTeam,
                    lobby.SuggestedRoomName,
                    "밤  /  탐색 스킬",
                    "밤 액티브는 키보드 1로 쓴다 (기획서 9.2)",
                    SelectCharacter, SelectTeam, StartOrReady, LeaveRoom);
                break;
        }
    }

    /// 방에서 이미 집어 간 칸. 매 갱신마다 리스트를 새로 만들지 않도록 재사용한다.
    readonly List<UICharacterSelectScreen.Claim> claims = new();

    /// 방의 모두가 서로의 픽을 본다 — 누가 무엇을 골랐는지는 이름표로 뜬다.
    /// 잠그는 것은 같은 팀의 픽뿐이다. 기획서 9.1의 중복 픽 금지가 팀 안에서만이라,
    /// 다른 팀의 픽까지 막으면 고를 수 있는 칸이 팀 수만큼 줄어든다.
    IReadOnlyList<UICharacterSelectScreen.Claim> Claims()
    {
        claims.Clear();
        var members = lobby.Members;
        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (member.IsSelf || !CharacterCatalog.IsValid(member.Character)) continue;

            claims.Add(new UICharacterSelectScreen.Claim(
                member.Character, member.Name, TeamColorOf(member.Team),
                member.Team == lobby.SelectedTeam));
        }
        return claims;
    }

    /// 집어 간 사람의 팀 색. 카드 테두리가 이 색이라, 방의 모두가 어느 팀이 무엇을
    /// 골랐는지 같은 색으로 본다.
    ///
    /// 팀을 아직 안 고른 사람(`TeamSeats.NoPreference`)은 팔레트 밖이라 패널 색으로 둔다.
    static Color TeamColorOf(int team)
    {
        var palette = UITheme.TeamColors;
        return team >= 0 && team < palette.Length ? TeamColors.Of(team) : UITheme.PanelDeep;
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
                            lobby.OccupancyOf, lobby.TeamHasRoom,
                            lobby.SelfReady, lobby.ReadyCount, lobby.ReadyTotal);
                break;

            // 대기실이 곧 이 화면이다. 남의 픽과 준비 수는 스팀 로비 멤버 데이터가
            // 바뀔 때마다 `SteamLobby.Changed`를 타고 여기로 온다.
            case UICharacterSelectScreen pick:
                pick.SetClaims(Claims());
                pick.SetRoster(lobby.Members);
                pick.SetLobby(lobby.IsRoomHost, lobby.CanStartMatch, lobby.SelfReady,
                              lobby.ReadyCount, lobby.ReadyTotal);
                break;
        }
    }
}
