/// 밤의 확정 수치 (기획서 6장). 밤 컴포넌트가 직렬화 필드 대신 이 표를 읽는다.
public static class NightBalance
{
    /// 가방 100% 기준선 (기획서 6.7).
    public static readonly float BagCapacity = 10f;

    /// 박스 개봉 홀드 (기획서 6.5.2).
    public static readonly float BoxOpenSeconds = 1.5f;

    /// 묻은 가방 회수·소각 홀드 (기획서 6.7).
    public static readonly float BagRetrieveSeconds = 1.5f;
    public static readonly float BagBurnSeconds = 3f;

    /// 적재 80% 이상인 상대에게 대시하면 총 적재의 이 비율이 떨어진다 (기획서 6.6).
    public static readonly float DashSpillShare = 0.3f;
}
