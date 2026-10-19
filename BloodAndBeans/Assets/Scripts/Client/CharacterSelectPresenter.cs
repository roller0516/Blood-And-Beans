using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// 캐릭터 선택 씬의 화면 흐름과 SteamLobby 연동. 방에 들어가 있는 동안의 픽·팀·준비·시작을 맡는다.
///
/// 대기실을 따로 거치지 않고 픽·준비·시작을 한 화면에서 본다. 화면 프리팹을 이어 두지 않았으면
/// 예전 대기실(`UIRoomScreen`)로 물러난다 — 여기서 멈추면 방에 들어가고도 아무것도 못 한다.
public sealed class CharacterSelectPresenter
{
    readonly UIManager ui;
    readonly SteamLobby lobby;
    readonly GameSceneManager scenes;
    readonly CharacterStage stage;

    bool active;

    public CharacterSelectPresenter(UIManager ui, SteamLobby lobby, GameSceneManager scenes, CharacterStage stage)
    {
        this.ui = ui;
        this.lobby = lobby;
        this.scenes = scenes;
        this.stage = stage;
    }

    /// 첫 화면을 연다. 화면이 다 뜨면 끝난다.
    public async UniTask EnterAsync()
    {
        if (active) return;

        active = true;
        lobby.Changed += Render;
        lobby.MatchStarting += OnMatchStarting;

        SoundManager.Instance.PlayBgm(Bgm.Lobby);
        if (await OpenScreenAsync<UICharacterSelectScreen>() != null)
        {
            Render();
            return;
        }

        if (!active) return;
        var room = await OpenScreenAsync<UIRoomScreen>();
        if (room == null) return;
        room.BuildTeams(lobby.TeamCount);
        Render();
    }

    public void Disable()
    {
        if (!active) return;

        active = false;
        lobby.Changed -= Render;
        lobby.MatchStarting -= OnMatchStarting;
    }

    /// 방장이 매치를 열었다. 손님은 이미 캐릭터를 고른 상태이므로 바로 붙는다.
    void OnMatchStarting()
    {
        if (!active) return;
        lobby.JoinStartedMatch();
    }

    /// 카드를 눌렀다. 픽은 스팀 로비 멤버 데이터로 바로 나가 같은 방의 모두가 본다
    /// (`SteamLobby.SelectCharacter`).
    public void SelectCharacter(int index) => lobby.SelectCharacter(index);

    public void SelectTeam(int team) => lobby.SelectTeam(team);

    /// 방장의 「게임 시작」(예전 대기실 화면).
    public void StartMatch() => lobby.StartMatch();

    /// 캐릭터 선택 화면 바닥의 큰 버튼. 방장에게는 「게임 시작」이고 나머지에게는
    /// 「준비」다 (기획서 10.1). 글자는 `UICharacterSelectScreen.SetLobby`가 그린다.
    ///
    /// 접속보다 픽이 먼저여야 한다. `PlayerCharacter.OnNetworkSpawn`이
    /// `GameManager.SelectedCharacter`를 읽어 서버로 올리기 때문이다 — 픽은 이미
    /// 로비에 들어가 있으므로 여기서는 순서를 걱정하지 않는다.
    void StartOrReady()
    {
        if (!lobby.IsRoomHost) { lobby.ToggleReady(); return; }
        if (lobby.CanStartMatch) { lobby.StartMatch(); return; }

        // 방장의 시작 버튼은 잠그지 않는다. 눌렀는데 못 시작하면 이유를 팝업으로 알린다.
        ShowMessageAsync(lobby.ReadyCount < lobby.ReadyTotal ? NotAllReadyMessage : TeamFullMessage).Forget();
    }

    const string NotAllReadyMessage = "모든 유저가 준비해야 게임을 시작할 수 있습니다.";
    const string TeamFullMessage = "팀 정원을 확인해 주세요.";

    async UniTaskVoid ShowMessageAsync(string text)
    {
        await ui.LoadAsync<UIMessagePopup>();
        if (!active || ui.CurrentPopup is UIMessagePopup) return;
        ui.PushPopup<UIMessagePopup>()?.Bind(text, ui.PopPopup);
    }

    /// 방을 나가면 타이틀 씬으로 돌아간다. 곡은 타이틀 화면이 열면서 바꾼다.
    public void LeaveRoom()
    {
        lobby.LeaveRoom();
        scenes.GoToTitle();
    }

    // --- 그리기 ---

    /// 처음 여는 화면은 프리팹을 불러와야 한다. 기다리는 사이 씬을 떠났으면 열지 않는다.
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
            case UIRoomScreen room:
                room.Bind(StartMatch, LeaveRoom, SelectTeam, lobby.ToggleReady);
                break;

            case UICharacterSelectScreen pick:
                pick.SetStage(stage);

                // 방의 누가 무엇을 골랐는지 보여 준다 (기획서 3.4). 최종 판정은 서버
                // 한 곳이다 (`PlayerCharacter.PickRpc`, 기획서 9.3) — 이건 표시일 뿐이다.
                // 셋째 인자는 팀이다. 캐릭터 선택창의 팀 칸이 곧 팀 선택이라 로비의
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

    /// 카드에는 내 팀의 픽만 뜬다. 다른 팀의 픽은 보여 주지도 잠그지도 않는다 —
    /// 기획서 9.1의 중복 픽 금지가 팀 안에서만이라서다.
    IReadOnlyList<UICharacterSelectScreen.Claim> Claims()
    {
        claims.Clear();
        var members = lobby.Members;
        for (var i = 0; i < members.Count; i++)
        {
            var member = members[i];
            if (member.IsSelf || member.Team != lobby.SelectedTeam
                || !CharacterCatalog.IsValid(member.Character)) continue;

            claims.Add(new UICharacterSelectScreen.Claim(
                member.Character, member.Name, TeamColorOf(member.Team), true));
        }
        return claims;
    }

    /// 집어 간 사람의 팀 색. 카드 테두리가 이 색이다.
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
                pick.SetLobby(lobby.IsRoomHost, lobby.SelfReady, lobby.ReadyCount, lobby.ReadyTotal);
                break;
        }
    }
}
