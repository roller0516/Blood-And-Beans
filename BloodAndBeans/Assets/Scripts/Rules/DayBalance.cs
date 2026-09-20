/// 기획서 v5.0 5.2·5.3·8.1의 확정값과 플레이 검증용 임시 밸런스.
public static class DayBalance
{
    public static float CoffeeSeconds => Balance.Current.CoffeeSeconds;
    public static float OvenSeconds => Balance.Current.OvenSeconds;
    public static float WashSeconds => Balance.Current.WashSeconds;
    public static int Machines => Balance.Current.Machines;
    public static int Sinks => Balance.Current.Sinks;
    // 기획서 5.5: 낮이 시작되면 이 간격으로 대기 슬롯 수만큼 들어오고, 그 뒤로는 자리가 비는 즉시 들어온다.
    public static float FirstEntrySeconds => Balance.Current.FirstEntrySeconds;
    // 기획서 5.2: Perfect는 그 손님의 최대 인내심을 이 비율만큼 회복시킨다.
    public static float PerfectPatienceRecovery => Balance.Current.PerfectPatienceRecovery;
    // 기획서 5.7.2: 인내심 링의 촉박·위급 경계.
    public static float PatienceWarning => Balance.Current.PatienceWarning;
    public static float PatienceUrgent => Balance.Current.PatienceUrgent;
}

