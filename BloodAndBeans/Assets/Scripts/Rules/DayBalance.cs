/// 기획서 v5.0 5.2·5.3·8.1의 확정값과 플레이 검증용 임시 밸런스.
public static class DayBalance
{
    public static readonly float CoffeeSeconds = 2f;
    public static readonly float OvenSeconds = 4f;
    public static readonly float WashSeconds = 3f;
    public static readonly int Machines = 3;
    public static readonly int Sinks = 3;
    public static readonly int BuffDays = 3;
    // 기획서 5.5: 낮이 시작되면 이 간격으로 대기 슬롯 수만큼 들어오고, 그 뒤로는 자리가 비는 즉시 들어온다.
    public static readonly float FirstEntrySeconds = 2f;
    // 기획서 5.2: Perfect는 그 손님의 최대 인내심을 이 비율만큼 회복시킨다.
    public static readonly float PerfectPatienceRecovery = 0.15f;
    // 기획서 5.7.2: 인내심 링의 촉박·위급 경계.
    public static readonly float PatienceWarning = 0.5f;
    public static readonly float PatienceUrgent = 0.2f;

    // ponytail: v5.0 임시 버프 수치. 8장 보석 6종 수치로 교체한다. 낮 스킬 수치는 `DaySkills`에 있다.
    public static readonly float BuffSpeed = 1.2f;
    public static readonly float BuffPerfect = 1.5f;
}

public enum TeamBuff { Dishes, Move, Perfect, Resistance, Wash, Cook, Finish, Serve }

/// 재료별 갱신은 중첩하지 않고 마지막 유효 일차 하나만 보관한다 (기획서 8.1).
public sealed class TeamBuffs
{
    readonly int[] lastDay = new int[8];
    public bool Apply(TeamBuff buff, int day)
    {
        var index = (int)buff;
        if (index < 0 || index >= lastDay.Length || day < 1) return false;
        lastDay[index] = day + DayBalance.BuffDays - 1;
        return true;
    }
    public int Remaining(TeamBuff buff, int day) => day < 1 || (int)buff < 0 || (int)buff >= lastDay.Length
        ? 0 : System.Math.Max(0, lastDay[(int)buff] - day + 1);
    public static readonly string[] Names = { "식기 +2", "이동 +20%", "Perfect 폭 +50%", "방해 면역", "세척 속도 +20%", "조리 속도 +20%", "마무리 속도 +20%", "서빙 거리 +20%" };
    public static readonly Ingredient[] Materials = { Ingredient.UpgradePart, Ingredient.MoveGem, Ingredient.PerfectGem,
        Ingredient.ResistGem, Ingredient.WashGem, Ingredient.CookGem, Ingredient.FinishGem, Ingredient.ServeGem };
    public static int IndexOf(Ingredient ingredient) => System.Array.IndexOf(Materials, ingredient);
}
