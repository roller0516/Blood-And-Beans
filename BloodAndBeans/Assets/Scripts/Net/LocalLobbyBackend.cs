using System;
using System.Collections.Generic;
using System.IO;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// 스팀 없이 도는 로비 (`NetPlatform.Editor`). 에디터 창 하나가 계정 하나다.
///
/// MPPM 가상 플레이어는 별도 프로세스라 정적 필드를 나눠 가질 수 없다. 그래서 사람마다
/// 파일 하나를 임시 폴더에 두고 서로 읽는다 — 각자 자기 파일만 쓰므로 잠글 것이 없다.
/// 방을 따로 두지도 않는다. 방장의 기록이 곧 방이고, 손님은 <see cref="Record.roomId"/>로
/// 그것을 가리킨다.
///
/// 정체는 프로세스 id다. 스팀 계정과 달리 창마다 다르므로, 같은 계정으로 창을 두 개
/// 띄웠을 때 서로를 자기 자신으로 착각하는 문제가 없다.
public sealed class LocalLobbyBackend : ILobbyBackend
{
    /// 다른 창의 기록을 보는 주기. 로비 화면은 사람이 보는 속도면 되고, 이 경로는 개발용이라
    /// 더 잘게 볼 이유가 없다.
    const float PollInterval = 0.3f;

    /// 이만큼 갱신이 없으면 죽은 창으로 본다. 재생을 멈춘 에디터는 하트비트를 쓰지 못한다.
    const double StaleSeconds = 5;

    /// 창 하나의 기록. 파일 하나에 통째로 들어간다.
    [Serializable]
    sealed class Record
    {
        public ulong id;

        /// 창 번호. 이름을 짓는 데만 쓴다.
        public int slot;

        public string name;

        /// 방장의 id. 0이면 방 밖이고, <see cref="id"/>와 같으면 내가 방장이다.
        public ulong roomId;

        // 아래 넷은 방장만 채운다. 손님의 값은 아무도 읽지 않는다.
        public string roomName;
        public int teams;
        public int capacity;
        public bool live;
        public ulong serverId;

        public int team;
        public bool ready;
        public int pick;
    }

    readonly string directory;
    readonly Record self = new();

    /// 지금 살아 있는 창들. 매 폴링마다 파일에서 다시 읽는다.
    readonly List<Record> alive = new();

    readonly List<LobbyRoom> rooms = new();
    readonly List<LobbyMember> members = new();

    string selfPath;
    float nextPoll;
    long signature;
    bool announced;

    public LocalLobbyBackend()
    {
        // 임시 폴더는 사용자 단위라 같은 PC의 에디터 창끼리는 같은 곳을 본다.
        directory = Path.Combine(Path.GetTempPath(), "BloodAndBeans.LocalLobby");
    }

    public bool Ready => selfPath != null;
    public ulong SelfId => self.id;
    public string SelfName => self.name;
    public string LastError { get; private set; } = string.Empty;

    public event Action Changed;
    public event Action<ulong> MatchStarted;

    public void Initialize()
    {
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception e)
        {
            LastError = $"로컬 로비 폴더를 만들지 못했다: {e.Message}";
            return;
        }

        self.id = (ulong)System.Diagnostics.Process.GetCurrentProcess().Id;
        self.team = TeamSeats.NoPreference;
        self.pick = CharacterCatalog.NoPick;

        selfPath = Path.Combine(directory, $"player-{self.id}.json");

        // 창 번호는 지금 살아 있는 창들 중 비어 있는 가장 작은 수다. 한 번 정하면 바꾸지
        // 않는다 — 남이 나갈 때마다 번호가 밀리면 화면의 이름이 흔들린다.
        // ponytail: 두 창이 같은 순간에 켜지면 같은 번호를 집을 수 있다. id는 프로세스 id라
        // 겹치지 않으므로 겹치는 것은 이름뿐이고, 개발용이라 그대로 둔다.
        Read();
        self.slot = FirstFreeSlot();
        self.name = $"에디터 {self.slot}";

        Write();
        LastError = string.Empty;
    }

    public void Pump()
    {
        if (!Ready || Time.realtimeSinceStartup < nextPoll) return;
        nextPoll = Time.realtimeSinceStartup + PollInterval;

        // 내 파일을 다시 써서 살아 있음을 알린다. 파일의 수정 시각 자체가 하트비트다.
        Write();

        var before = signature;
        Read();
        if (signature != before) Changed?.Invoke();

        NotifyMatchStart();
    }

    public void Shutdown()
    {
        if (selfPath == null) return;

        var path = selfPath;
        selfPath = null;

        try
        {
            File.Delete(path);
        }
        catch (Exception e)
        {
            // 지우지 못해도 몇 초 뒤 낡은 기록으로 사라진다. 남은 파일 하나 때문에 종료를
            // 막을 이유는 없다.
            CDebug.LogWarning($"{nameof(LocalLobbyBackend)}: 기록을 지우지 못했다: {e.Message}");
        }
    }

    // --- 파일 ---

    void Write()
    {
        if (selfPath == null) return;

        try
        {
            // 반쯤 쓰인 순간에 남이 읽으면 파싱이 깨진다. 읽는 쪽이 건너뛰고 다음 폴링에
            // 다시 읽으므로 여기서 임시 파일까지 쓰지는 않는다.
            File.WriteAllText(selfPath, JsonUtility.ToJson(self));
        }
        catch (Exception e)
        {
            LastError = $"로컬 로비 기록을 쓰지 못했다: {e.Message}";
        }
    }

    void Read()
    {
        alive.Clear();

        string[] files;
        try
        {
            files = Directory.GetFiles(directory, "player-*.json");
        }
        catch (Exception e)
        {
            LastError = $"로컬 로비 폴더를 읽지 못했다: {e.Message}";
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var path in files)
        {
            try
            {
                // 하트비트는 파일의 수정 시각이다. 재생을 멈춘 창은 곧 목록에서 사라진다.
                if ((now - File.GetLastWriteTimeUtc(path)).TotalSeconds > StaleSeconds) continue;

                var record = JsonUtility.FromJson<Record>(File.ReadAllText(path));
                if (record != null && record.id != 0) alive.Add(record);
            }
            catch (Exception)
            {
                // 남이 쓰는 중이었다. 다음 폴링에 다시 읽는다 — 여기서 실패를 남기면
                // 정상 동작이 오류로 보인다.
            }
        }

        alive.Sort((a, b) => a.id.CompareTo(b.id));
        signature = Signature();
    }

    /// 내용이 바뀌었는지만 본다. 파일 시각으로 재면 하트비트가 매번 「변경」이 돼,
    /// 아무도 아무것도 안 해도 화면이 0.3초마다 다시 그려진다.
    long Signature()
    {
        long hash = 17;
        for (var i = 0; i < alive.Count; i++)
        {
            var record = alive[i];
            hash = hash * 31 + (long)record.id;
            hash = hash * 31 + (long)record.roomId;
            hash = hash * 31 + (long)record.serverId;
            hash = hash * 31 + record.slot;
            hash = hash * 31 + record.teams;
            hash = hash * 31 + record.capacity;
            hash = hash * 31 + record.team;
            hash = hash * 31 + record.pick;
            hash = hash * 31 + (record.ready ? 1 : 0);
            hash = hash * 31 + (record.live ? 1 : 0);
        }
        return hash;
    }

    int FirstFreeSlot()
    {
        for (var slot = 1; ; slot++)
        {
            var used = false;
            for (var i = 0; i < alive.Count; i++)
                if (alive[i].slot == slot) { used = true; break; }

            if (!used) return slot;
        }
    }

    Record Find(ulong id)
    {
        for (var i = 0; i < alive.Count; i++)
            if (alive[i].id == id) return alive[i];
        return null;
    }

    /// 방장의 기록이 곧 방이다.
    Record Host() => self.roomId == 0 ? null : Find(self.roomId);

    int MemberCount(ulong roomId)
    {
        var count = 0;
        for (var i = 0; i < alive.Count; i++)
            if (alive[i].roomId == roomId) count++;
        return count;
    }

    // --- 방 목록 ---

    public UniTask<IReadOnlyList<LobbyRoom>> ListRoomsAsync(int limit)
    {
        Read();
        rooms.Clear();

        for (var i = 0; i < alive.Count && rooms.Count < limit; i++)
        {
            var record = alive[i];

            // 방장 자신의 기록만 방이다. 이미 시작한 방은 들어가 봐야 승인 전에 막힌다.
            if (record.roomId != record.id || record.live) continue;

            rooms.Add(new LobbyRoom(record.id, record.id, record.roomName,
                                    MemberCount(record.id), record.capacity));
        }

        LastError = string.Empty;
        return UniTask.FromResult<IReadOnlyList<LobbyRoom>>(rooms);
    }

    // --- 방 만들기 / 참가 ---

    public UniTask<bool> CreateRoomAsync(string roomName, int capacity, int teamCount)
    {
        self.roomId = self.id;
        self.roomName = roomName;
        self.teams = teamCount;
        self.capacity = capacity;
        self.live = false;
        self.serverId = 0;
        announced = false;

        Write();
        Read();

        LastError = string.Empty;
        return UniTask.FromResult(true);
    }

    public UniTask<bool> JoinRoomAsync(LobbyRoom room)
    {
        Read();

        var host = Find(room.HostId);
        if (host == null || host.roomId != host.id || host.live)
        {
            LastError = "방이 사라졌다.";
            return UniTask.FromResult(false);
        }

        // 스팀은 로비 정원을 스스로 막지만 여기서는 막을 사람이 없다. 최종 판정은 접속
        // 승인(`MatchSeating`)이 하고, 이건 들어가 보나 마나인 경우를 먼저 걸러 낸다.
        if (MemberCount(host.id) >= host.capacity)
        {
            LastError = "방이 가득 찼다.";
            return UniTask.FromResult(false);
        }

        self.roomId = host.id;
        self.live = false;
        self.serverId = 0;
        announced = false;

        Write();
        Read();

        LastError = string.Empty;
        return UniTask.FromResult(true);
    }

    public void LeaveRoom()
    {
        if (selfPath == null) return;

        self.roomId = 0;
        self.roomName = null;
        self.live = false;
        self.serverId = 0;
        self.ready = false;
        announced = false;

        Write();
    }

    // --- 대기실 ---

    public bool InRoom => self.roomId != 0;
    public bool IsHost => self.roomId != 0 && self.roomId == self.id;
    public ulong RoomHostId => self.roomId;

    public string RoomName => IsHost ? self.roomName : Host()?.roomName ?? string.Empty;
    public int RoomTeamCount => IsHost ? self.teams : Host()?.teams ?? 0;

    public IReadOnlyList<LobbyMember> ReadMembers()
    {
        members.Clear();
        if (self.roomId == 0) return members;

        for (var i = 0; i < alive.Count; i++)
        {
            var record = alive[i];
            if (record.roomId != self.roomId) continue;

            members.Add(new LobbyMember(record.id, record.name, record.team,
                                        record.ready, record.pick));
        }
        return members;
    }

    public void WriteSelf(int team, bool ready, int character)
    {
        self.team = team;
        self.ready = ready;
        self.pick = character;

        Write();
        Read();
    }

    // --- 시작 ---

    public void CloseRoom()
    {
        self.live = true;
        Write();
    }

    public void ReopenRoom()
    {
        if (!IsHost) return;
        self.live = false;
        self.serverId = 0;
        Write();
    }

    public void AnnounceServer()
    {
        self.serverId = self.id;
        Write();
    }

    /// 방장의 기록에 서버가 적히면 손님이 붙는다. 방장 자신은 시작 버튼이 곧 그 신호라
    /// 여기서 오르지 않는다.
    void NotifyMatchStart()
    {
        if (!InRoom || IsHost) return;

        var host = Host();
        if (host == null || host.serverId == 0) { announced = false; return; }
        if (announced) return;

        announced = true;
        MatchStarted?.Invoke(host.serverId);
    }
}
