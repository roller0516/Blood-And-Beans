using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

/// 방 목록의 한 줄. 백엔드가 자기 방식으로 채운다.
public readonly struct LobbyRoom
{
    public LobbyRoom(ulong id, ulong hostId, string name, int members, int capacity)
    {
        Id = id;
        HostId = hostId;
        Name = name;
        Members = members;
        Capacity = capacity;
    }

    public ulong Id { get; }
    public ulong HostId { get; }
    public string Name { get; }
    public int Members { get; }
    public int Capacity { get; }
}

/// 방 안의 한 사람. 문자열로 오가는 값은 백엔드가 풀어서 준다.
public readonly struct LobbyMember
{
    public LobbyMember(ulong id, string name, int team, bool ready, int character)
    {
        Id = id;
        Name = name;
        Team = team;
        Ready = ready;
        Character = character;
    }

    public ulong Id { get; }
    public string Name { get; }

    /// 아직 고르지 않았으면 `TeamSeats.NoPreference`.
    public int Team { get; }

    public bool Ready { get; }

    /// 고른 캐릭터. `CharacterCatalog.NoPick`이면 아직 안 골랐다.
    public int Character { get; }

    public static int ParseTeam(string raw) =>
        int.TryParse(raw, out var team) ? team : TeamSeats.NoPreference;

    public static int ParsePick(string raw) =>
        int.TryParse(raw, out var pick) && CharacterCatalog.IsValid(pick)
            ? pick : CharacterCatalog.NoPick;
}

/// 방 목록과 대기실을 어디에 두는가. 스팀 로비(<see cref="SteamLobbyBackend"/>)와 에디터
/// 창끼리의 가짜 로비(<see cref="LocalLobbyBackend"/>)가 이 자리를 나눠 쓴다.
///
/// 팀 정원·준비 판정·좌석·접속은 <see cref="SteamLobby"/>에 그대로 있다. 여기 있는 것은
/// "누가 어느 방에 있고 무엇을 골랐는가"를 어디서 읽고 쓰느냐뿐이다 — 규칙이 백엔드마다
/// 갈리면 스팀에서만 나거나 에디터에서만 나는 결함이 생긴다.
public interface ILobbyBackend
{
    /// 방 흐름을 쓸 수 있는가. false면 목록도 만들기도 되지 않는다.
    bool Ready { get; }

    ulong SelfId { get; }
    string SelfName { get; }

    /// 마지막 실패 사유. 성공한 호출은 비운다.
    string LastError { get; }

    /// 방 목록·멤버·멤버 데이터 중 무엇이든 바뀌었다.
    event Action Changed;

    /// 방장이 매치를 열었다. 인자는 붙을 서버의 id다.
    event Action<ulong> MatchStarted;

    void Initialize();

    /// 매 프레임. 스팀은 콜백을 펌프하고, 로컬은 다른 창의 기록을 본다.
    void Pump();

    void Shutdown();

    /// 실패하면 null. 사유는 <see cref="LastError"/>에 있다.
    UniTask<IReadOnlyList<LobbyRoom>> ListRoomsAsync(int limit);

    /// 방을 만들고 그 방에 들어간다.
    UniTask<bool> CreateRoomAsync(string roomName, int capacity, int teamCount);

    UniTask<bool> JoinRoomAsync(LobbyRoom room);

    void LeaveRoom();

    bool InRoom { get; }
    bool IsHost { get; }
    ulong RoomHostId { get; }
    string RoomName { get; }

    /// 방을 만든 사람이 고른 팀 수 (기획서 10장). 모르면 0.
    int RoomTeamCount { get; }

    IReadOnlyList<LobbyMember> ReadMembers();

    /// 내 팀·준비·픽을 방에 적는다. 같은 방의 모두가 곧바로 본다.
    void WriteSelf(int team, bool ready, int character);

    /// 방장 전용. 방을 잠가 목록에서 뺀다. 서버는 아직 뜨지 않았다.
    void CloseRoom();

    /// 방장 전용. 「여기로 붙어라」를 알린다. 서버가 뜬 뒤에 부른다 — 먼저 알리면 손님이
    /// 아직 없는 서버로 접속한다.
    void AnnounceServer();
}
