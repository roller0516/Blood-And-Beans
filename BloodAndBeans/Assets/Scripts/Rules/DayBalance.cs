/// 기획서 v5.0 5.2·5.3·8.1의 확정값과 플레이 검증용 임시 밸런스.
public static class DayBalance
{
    public static readonly float CoffeeSeconds = 2f;
    public static readonly float OvenSeconds = 4f;
    public static readonly float WashSeconds = 3f;
    public static readonly int Machines = 3;
    public static readonly int Sinks = 3;
    public static readonly int BuffDays = 3;

    // ponytail: 14장 #41~43 미결. 임시 플레이를 사용자 승인으로 적용하며 확정 시 이 표를 교체한다.
    public static readonly float BuffSpeed = 1.2f;
    public static readonly float BuffPerfect = 1.5f;
    public static readonly float SkillSeconds = 4f;
    public static readonly float SkillCooldown = 35f;
    public static readonly float SkillReach = 8f;
    public static readonly float SlowScale = 0.65f;
    public static readonly string[] SkillNames = { "끈적한 발걸음", "엉킨 손", "흔들리는 집중", "거품 장난", "긴장한 마무리" };
    public static readonly string[] SkillEffects = { "가까운 상대 이동속도 35% 감소", "가까운 상대 조리 시간 50% 증가", "가까운 상대 Perfect 폭 50% 감소", "가까운 상대 세척 시간 50% 증가", "가까운 상대 마무리 시간 50% 증가" };
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
