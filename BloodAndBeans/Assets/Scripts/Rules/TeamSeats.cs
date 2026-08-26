/// 팀별 좌석 명부. "이 팀에 자리가 남았는가"와 "이 사람을 어느 팀에 앉힐 것인가"만 답한다.
///
/// 로비가 생기기 전까지 `MatchDirector`는 입장 순서 라운드 로빈으로만 앉혔고, 팀 정원이라는
/// 개념 자체가 없었다. 로비에서 팀을 고르게 하려면 "팀 2가 이미 찼다"를 접속 승인 시점에
/// 답할 수 있어야 한다 (기획서 10장: 팀 인원은 로비가 정한다).
///
/// 순수 C#이라 씬 없이 검증한다. 정원 초과 거절은 서버 권위의 마지막 방어선이므로
/// 여기서 틀리면 클라이언트가 보낸 팀 번호가 그대로 통과한다.
public class TeamSeats
{
    /// 앉힐 자리가 없다. 접속을 거절해야 한다는 뜻이다.
    public const int NoSeat = -1;

    /// 선호 팀이 없다. 가장 빈 팀에 앉힌다.
    public const int NoPreference = -1;

    readonly int[] occupancy;

    public TeamSeats(int teamCount, int capacityPerTeam)
    {
        occupancy = new int[teamCount > 0 ? teamCount : 1];
        CapacityPerTeam = capacityPerTeam > 0 ? capacityPerTeam : 1;
    }

    public int TeamCount => occupancy.Length;
    public int CapacityPerTeam { get; }
    public int Capacity => TeamCount * CapacityPerTeam;

    public int OccupancyOf(int team) => InRange(team) ? occupancy[team] : 0;
    public bool HasRoom(int team) => InRange(team) && occupancy[team] < CapacityPerTeam;

    /// 한 명을 앉힌다. 선호 팀에 자리가 있으면 그 팀, 아니면 가장 빈 팀. 만원이면 `NoSeat`.
    ///
    /// 선호가 없을 때 "가장 빈 팀"인 이유는 2인 세션이 같은 팀에 몰리면 팀 격리를 시험할 수
    /// 없기 때문이다. 인원이 같으면 낮은 번호가 이기므로 예전 라운드 로빈과 결과가 같다.
    public int Take(int preferred)
    {
        var team = HasRoom(preferred) ? preferred : Emptiest();
        if (team == NoSeat) return NoSeat;

        occupancy[team]++;
        return team;
    }

    /// 한 명이 나갔다. 앉힌 적 없는 팀을 비우려 들면 아무 일도 하지 않는다.
    public void Release(int team)
    {
        if (InRange(team) && occupancy[team] > 0) occupancy[team]--;
    }

    public void Clear()
    {
        for (var i = 0; i < occupancy.Length; i++) occupancy[i] = 0;
    }

    int Emptiest()
    {
        var best = NoSeat;
        for (var team = 0; team < occupancy.Length; team++)
        {
            if (occupancy[team] >= CapacityPerTeam) continue;
            if (best == NoSeat || occupancy[team] < occupancy[best]) best = team;
        }
        return best;
    }

    bool InRange(int team) => team >= 0 && team < occupancy.Length;
}
