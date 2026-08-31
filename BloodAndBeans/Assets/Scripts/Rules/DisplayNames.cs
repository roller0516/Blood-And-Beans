/// 사용자에게 보이는 이름. 재료와 손님 종족의 한글 표기를 한 곳에 모은다.
///
/// 화면마다 문자열을 박아 두면 같은 재료가 화면에 따라 다른 이름으로 나온다. 규칙과
/// 같은 어셈블리에 두는 이유는 이름의 출처가 기획서 표이고(7.1 재료 · 5.5 손님 종족),
/// 그 표를 읽는 쪽이 규칙이기 때문이다.
public static class DisplayNames
{
    // 기획서 7.1 재료 표. 열거자 순서와 같아야 한다.
    static readonly string[] IngredientNames =
    {
        "우유", "크림", "초콜렛", "아몬드", "베리", "얼음",
        "블러드 빈", "업그레이드 재료", "원두", "빵 베이스",
    };

    // 기획서 5.5 손님 종족 표. `Race` 열거자 순서와 같아야 한다.
    static readonly string[] RaceNames =
    {
        "좀비", "뱀파이어", "유령", "해골", "늑대인간", "마녀",
    };

    public static string Of(Ingredient item)
    {
        var i = (int)item;
        return i >= 0 && i < IngredientNames.Length ? IngredientNames[i] : "—";
    }

    public static string Of(Race race)
    {
        var i = (int)race;
        return i >= 0 && i < RaceNames.Length ? RaceNames[i] : "—";
    }

    /// 팀 표기. 내부 인덱스는 0부터지만 화면에는 1부터 센 번호를 보인다.
    /// 배정 전(음수)에 번호를 찍으면 「0팀」이 나오므로 대기 문구를 따로 준다.
    public static string Team(int team) => team < 0 ? "팀 배정 대기" : $"{team + 1}팀";
}
