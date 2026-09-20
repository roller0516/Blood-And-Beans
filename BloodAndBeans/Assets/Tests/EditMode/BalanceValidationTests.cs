using NUnit.Framework;

/// `BalanceData`의 표 정렬 검사 (`BalanceValidation`).
///
/// 표 대부분이 열거자 순서에 정렬돼 있는데 그 계약이 주석에만 있고, 어긋나도 컴파일
/// 오류가 나지 않는다. 값의 원본이 `Assets/Excel/BloodAndBeans.xlsx`라 기획자의 행 편집이
/// 그대로 들어온다 — 그래서 빌드 전에 여기서 막는다.
public class BalanceValidationTests
{
    // --- 지금 값이 정렬돼 있는가 ---

    [Test]
    public void FallbackTablesAreAligned()
    {
        var problems = BalanceValidation.Problems(new BalanceData());
        Assert.IsEmpty(problems, "기획서 확정치 폴백이 어긋났다: " + string.Join(" / ", problems));
    }

    [Test]
    public void LoadedTablesAreAligned()
    {
        // 에디터 부팅이 엑셀 애셋을 실어 두었으면 그 값이, 아니면 폴백이 검사된다.
        var problems = BalanceValidation.Problems(Balance.Current);
        Assert.IsEmpty(problems, "적용 중인 표가 어긋났다: " + string.Join(" / ", problems));
    }

    // --- 검사기가 실제로 잡는가 ---
    //
    // 위 두 테스트는 검사기가 아무것도 안 해도 통과한다. 아래가 없으면 통과에 의미가 없다.

    [Test]
    public void ShorterTableIsCaught()
    {
        // 엑셀에서 마지막 행을 지운 모양. 배열이 짧아져 Pick이 조용히 폴백한다.
        var data = new BalanceData { NightSkillCooldown = new[] { 0f, 18f, 18f } };

        Assert.IsNotEmpty(BalanceValidation.Problems(data), "짧아진 표를 잡지 못했다");
    }

    [Test]
    public void HoleInTheMiddleIsCaught()
    {
        // 엑셀에서 **중간** 행을 지운 모양. 길이는 그대로고 그 칸만 기본값으로 남는다.
        var data = new BalanceData();
        data.DaySkillNames[3] = null;

        Assert.IsNotEmpty(BalanceValidation.Problems(data), "중간에 빈 칸을 잡지 못했다");
    }

    [Test]
    public void ZeroCooldownIsCaught()
    {
        // 쿨타임 0은 그 스킬이 무한 연타가 된다는 뜻이다. [0]은 None이라 예외다.
        var data = new BalanceData();
        data.DaySkillCooldown[2] = 0f;

        Assert.IsNotEmpty(BalanceValidation.Problems(data), "0 쿨타임을 잡지 못했다");
    }

    [Test]
    public void DuplicateNightSkillIsCaught()
    {
        // 캐릭터마다 밤 액티브가 하나씩이다 (기획서 9.1.1). 겹치면 한 스킬이 화면에서 사라진다.
        var data = new BalanceData();
        data.Characters[1].Night = data.Characters[0].Night;

        Assert.IsNotEmpty(BalanceValidation.Problems(data), "중복된 밤 액티브를 잡지 못했다");
    }

    [Test]
    public void BrokenZoneSharesAreCaught()
    {
        // 구역 배분은 퍼센트라 합이 100이어야 한다 (기획서 6.3.1).
        var data = new BalanceData { ZoneShares = new[] { 45, 35, 30 } };

        Assert.IsNotEmpty(BalanceValidation.Problems(data), "100이 아닌 배분을 잡지 못했다");
    }
}
