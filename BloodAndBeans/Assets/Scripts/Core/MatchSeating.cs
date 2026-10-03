using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// "이 판에 팀이 몇이고 누가 어느 팀에 앉는가"에 답하는 단 하나의 권위.
///
/// 순수 C#이다. MonoBehaviour였을 때 필요했던 것은 직렬화 튜닝 값과 전역 접근뿐이었고,
/// 둘 다 소유자인 `SteamLobby`가 대신할 수 있다. 씬 오브젝트가 아니게 되면서 "루트여야
/// DontDestroyOnLoad가 된다" 같은 제약도 함께 사라졌다.
///
/// 수명은 `SteamLobby`와 같다. 그래야 하는 이유는 타이밍이다 — 클라이언트는 게임 씬이
/// 로드되기 *전에*, 타이틀에서 접속한다. 접속 승인이 게임 씬 오브젝트에 걸려 있으면 승인
/// 콜백이 없는 채로 접속이 들어와 타임아웃으로 죽는다.
///
/// 팀 번호의 출처는 하나다. `MatchDirector.TeamCount`도 이 값을 되읽는다.
public class MatchSeating
{
    /// `SetForcedSeatCheat`에 이 값을 주면 입장 순서대로 빈 팀에 앉힌다.
    public const int NoForcedSeat = -1;

    /// 기획서 10장이 지원하는 최대 팀 수(2팀 4인 / 3팀 6인 / 4팀 8인). 치트 툴은 이 위로
    /// 올릴 수 없다. 팀마다 `CafeTeam{n}` 레이어가 있어야 카메라 컬링까지 맞는다.
    readonly int maxTeams;

    /// 한 팀에 앉힐 수 있는 인원. 방 정원(팀 수 × 이 값)의 출처이기도 하다.
    readonly int playersPerTeam;

    /// 정원이 차서 접속을 거절할 때 클라이언트에게 보내는 사유.
    readonly string roomFullMessage;

    /// 접속 승인에서 잡아 둔 좌석. 플레이어가 스폰될 때 이 값을 그대로 쓴다. 승인 시점에
    /// 자리를 잡지 않으면 두 클라이언트가 같은 마지막 자리를 동시에 통과할 수 있다.
    readonly Dictionary<ulong, int> reservedSeats = new();

    readonly Dictionary<ulong, int> characters = new();
    public int CharacterOf(ulong clientId) => characters.TryGetValue(clientId, out var pick) ? pick : CharacterCatalog.NoPick;

    /// 접속 승인 때 받은 닉네임 (기획서 5.7.6-a 이름표). 로비를 거치지 않은 시작이면 비어 있다.
    readonly Dictionary<ulong, string> names = new();
    public string NameOf(ulong clientId) => names.TryGetValue(clientId, out var name) ? name : string.Empty;

    /// ponytail: 기획서에 닉네임 길이 규칙이 없다. 이름표 폭이 넘치지 않게 임시로 자른다 — 정해지면 BB.Rules 표로 옮긴다.
    const int MaxNameLength = 16;

    /// 페이로드 머리(팀 · 캐릭터 · 데이터 해시)의 길이. 닉네임은 그 뒤에 UTF-8로 붙는다.
    const int PayloadHeader = sizeof(int) * 3;

    TeamSeats seats;
    int forcedSeat = NoForcedSeat;

    /// 구독해 둔 매니저. 해제할 때 같은 것을 써야 한다 — 다른 인스턴스에서 해제하면
    /// 승인 콜백이 남아 죽은 좌석표를 계속 부른다.
    NetworkManager subscribed;

    public MatchSeating(int teams, int maxTeams, int playersPerTeam, string roomFullMessage)
    {
        this.maxTeams = Mathf.Max(1, maxTeams);
        this.playersPerTeam = Mathf.Max(1, playersPerTeam);
        this.roomFullMessage = roomFullMessage;

        ApplyTeamCount(Mathf.Clamp(teams, 1, this.maxTeams));
    }

    public int TeamCount { get; private set; }
    public int MaxTeams => maxTeams;
    public int PlayersPerTeam => playersPerTeam;
    public int Capacity => TeamCount * playersPerTeam;
    public int ForcedSeat => forcedSeat;

    /// 이 판에 들어오기로 한 인원(방장 포함). 첫 밤은 이만큼 모여야 시작한다 (`GamePhase`).
    /// 0이면 모른다 — 로비를 거치지 않은 시작이라 기다리지 않는다.
    public int ExpectedPlayers { get; private set; }

    /// 방장이 시작을 누른 순간의 방 인원을 적는다.
    public void ExpectPlayers(int count) => ExpectedPlayers = Mathf.Max(0, count);

    /// 접속 승인과 퇴장 처리를 건다. 여러 번 불러도 안전하다.
    ///
    /// 승인 콜백은 `StartHost`보다 먼저 걸려 있어야 한다. 늦으면 호스트는 멀쩡히 뜨고
    /// 손님만 승인 타임아웃으로 조용히 못 들어온다 — 서버 콘솔에 경고 한 줄만 남는다.
    public void Subscribe(NetworkManager manager)
    {
        if (manager == null || subscribed == manager) return;

        Unsubscribe();

        manager.OnClientDisconnectCallback += ReleaseSeatServer;
        if (manager.ConnectionApprovalCallback == null)
            manager.ConnectionApprovalCallback = ApproveConnectionServer;

        subscribed = manager;
    }

    public void Unsubscribe()
    {
        if (subscribed == null) return;

        subscribed.OnClientDisconnectCallback -= ReleaseSeatServer;
        if (subscribed.ConnectionApprovalCallback == ApproveConnectionServer)
            subscribed.ConnectionApprovalCallback = null;

        subscribed = null;
    }

    /// 팀 수를 바꾸면 좌석표도 새로 만든다.
    void ApplyTeamCount(int count)
    {
        TeamCount = count;
        seats = new TeamSeats(TeamCount, playersPerTeam);
        reservedSeats.Clear();
        characters.Clear();
        names.Clear();
        ExpectedPlayers = 0;
        if (forcedSeat >= TeamCount) forcedSeat = NoForcedSeat;
    }

    /// 판이 끝나고 다음 판을 시작할 때 좌석을 비운다. 이 객체는 씬을 넘어 살아남으므로,
    /// 씬을 다시 불러도 저절로 비워지지 않는다.
    public void ResetForNewMatch() => ApplyTeamCount(TeamCount);

    /// 접속 승인에서 정한 좌석을 돌려준다 (기획서 10장: 팀 인원은 로비가 정한다).
    ///
    /// 승인이 꺼져 있는 경로로 들어온 클라이언트는 예약이 없으므로 여기서 앉힌다. 그
    /// 경우에도 정원은 지킨다. 자리가 없으면 `TeamSeats.NoSeat`이다 — 실패를 팀 0으로
    /// 바꾸면 정원 초과가 남의 팀 설비 접근 권한으로 둔갑한다.
    public int SeatServer(ulong clientId)
    {
        if (reservedSeats.TryGetValue(clientId, out var reserved)) return reserved;

        var seat = seats.Take(forcedSeat >= 0 ? forcedSeat : TeamSeats.NoPreference);
        if (seat != TeamSeats.NoSeat) reservedSeats[clientId] = seat;
        return seat;
    }

    /// 클라이언트가 고른 팀을 접속 승인 페이로드로 싣고 푸는 단 한 쌍. 로비와 서버가 같은
    /// 형식을 쓰게 하려고 여기 둔다.
    public static byte[] EncodeTeamRequest(int team, int character = CharacterCatalog.NoPick, string name = null)
    {
        var nameBytes = System.Text.Encoding.UTF8.GetBytes(CleanName(name));
        var payload = new byte[PayloadHeader + nameBytes.Length];
        BitConverter.GetBytes(team).CopyTo(payload, 0);
        BitConverter.GetBytes(character).CopyTo(payload, sizeof(int));
        BitConverter.GetBytes(DataManager.ContentHash).CopyTo(payload, sizeof(int) * 2);
        nameBytes.CopyTo(payload, PayloadHeader);
        return payload;
    }

    static string DecodeName(byte[] payload) =>
        payload != null && payload.Length > PayloadHeader
            ? CleanName(System.Text.Encoding.UTF8.GetString(payload, PayloadHeader, payload.Length - PayloadHeader))
            : string.Empty;

    /// 클라이언트가 보낸 이름이라 서버가 다시 다듬는다. 제어 문자를 빼고 길이를 자른다.
    static string CleanName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;

        var clean = new System.Text.StringBuilder(name.Length);
        foreach (var c in name)
            if (!char.IsControl(c)) clean.Append(c);

        var trimmed = clean.ToString().Trim();
        if (trimmed.Length <= MaxNameLength) return trimmed;

        // 서로게이트 쌍 가운데서 자르면 깨진 글자가 남는다.
        var cut = char.IsHighSurrogate(trimmed[MaxNameLength - 1]) ? MaxNameLength - 1 : MaxNameLength;
        return trimmed.Substring(0, cut);
    }

    /// 호스트와 표가 다르면 코인 예고와 서버 정산이 어긋난다 (남은 작업 설계 1단계).
    const string DataMismatchMessage = "데이터 표가 호스트와 다르다. 같은 빌드로 다시 접속한다.";
    const string DataNotLoadedMessage = "호스트의 데이터 표가 실리지 않았다. 호스트 콘솔의 DataManager 오류를 확인한다.";

    static bool SameData(byte[] payload) =>
        payload != null && payload.Length >= sizeof(int) * 3 &&
        BitConverter.ToInt32(payload, sizeof(int) * 2) == DataManager.ContentHash;

    /// 로비를 거치지 않는 개발용 시작(자동 시작·개발 콘솔)이 부른다. 표가 실리지 않았으면
    /// 시작하지 않고, 페이로드가 비어 있으면 데이터 해시를 실어 둔다. 시작해도 되면 true.
    public static bool PrepareDirectStart(NetworkManager manager)
    {
        if (!Balance.Loaded)
        {
            CDebug.LogError("데이터 표가 실리지 않아 접속을 시작하지 않는다. 콘솔의 DataManager 오류를 확인한다.");
            return false;
        }

        if (manager.NetworkConfig.ConnectionData == null || manager.NetworkConfig.ConnectionData.Length == 0)
            manager.NetworkConfig.ConnectionData = EncodeTeamRequest(TeamSeats.NoPreference);
        return true;
    }

    public static int DecodeTeamRequest(byte[] payload) =>
        payload != null && payload.Length >= sizeof(int)
            ? BitConverter.ToInt32(payload, 0)
            : TeamSeats.NoPreference;

    /// 서버 전용. 클라이언트가 고른 팀에 자리가 있으면 그 팀, 없으면 가장 빈 팀에 앉히고,
    /// 방이 가득 찼으면 거절한다. 클라이언트가 보낸 값을 검사하는 유일한 지점이다.
    void ApproveConnectionServer(NetworkManager.ConnectionApprovalRequest request,
                                 NetworkManager.ConnectionApprovalResponse response)
    {
        // 호스트 자신은 서버와 같은 표다. NGO는 호스트 거절을 무시하고 플레이어 생성만 막으므로 검사하지 않는다.
        var isHost = request.ClientNetworkId == NetworkManager.ServerClientId;
        if (!isHost && (DataManager.ContentHash == 0 || !SameData(request.Payload)))
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Reason = DataManager.ContentHash == 0 ? DataNotLoadedMessage : DataMismatchMessage;
            return;
        }

        var requested = forcedSeat >= 0 ? forcedSeat : DecodeTeamRequest(request.Payload);
        var seat = seats.Take(requested);

        if (seat == TeamSeats.NoSeat)
        {
            response.Approved = false;
            response.CreatePlayerObject = false;
            response.Reason = roomFullMessage;
            return;
        }

        var pick = request.Payload != null && request.Payload.Length >= sizeof(int) * 2
            ? BitConverter.ToInt32(request.Payload, sizeof(int)) : CharacterCatalog.NoPick;
        if (!CharacterCatalog.IsValid(pick)) pick = CharacterCatalog.NoPick;
        foreach (var entry in reservedSeats)
            if (entry.Value == seat && CharacterOf(entry.Key) == pick) pick = CharacterCatalog.NoPick;
        characters[request.ClientNetworkId] = pick;
        names[request.ClientNetworkId] = DecodeName(request.Payload);
        reservedSeats[request.ClientNetworkId] = seat;
        response.Approved = true;
        response.CreatePlayerObject = true;
    }

    /// 나간 사람의 자리를 비운다. 이게 없으면 재접속만으로 방이 영영 가득 찬다.
    void ReleaseSeatServer(ulong clientId)
    {
        if (subscribed == null || !subscribed.IsServer) return;
        if (!reservedSeats.TryGetValue(clientId, out var seat)) return;

        reservedSeats.Remove(clientId);
        characters.Remove(clientId);
        names.Remove(clientId);
        seats.Release(seat);
    }

    /// 개발용. 다음에 들어오는 클라이언트부터 이 팀에 앉힌다. 이미 붙어 있는 플레이어의
    /// 팀은 바뀌지 않는다 — 팀이 바뀌면 복제 범위와 안개 소속을 다시 잡아야 하는데,
    /// 그건 재접속으로 얻는 편이 확실하다.
    public void SetForcedSeatCheat(int seat) =>
        forcedSeat = seat < 0 ? NoForcedSeat : seat % Mathf.Max(1, TeamCount);

    /// 개발용. 카페는 서버가 뜰 때 딱 한 번 스폰되므로 접속을 끊은 상태에서만 바꿀 수 있다.
    /// 성공하면 true. 접속 중이라 거절했으면 false.
    public bool SetTeamCountCheat(int count)
    {
        if (subscribed != null && subscribed.IsListening) return false;

        ApplyTeamCount(Mathf.Clamp(count, 1, maxTeams));
        return true;
    }
}
