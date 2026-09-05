/// 하루의 구조 (기획서 4장). 페이즈 길이와 한 판 일수는 기획서가 확정한 값이므로
/// 씬 직렬화가 아니라 여기에 있다.
///
/// 씬에 두면 씬 파일이 진실의 원천이 된다 — 실제로 `Battle_01.unity`의 `totalDays`가
/// 3으로 어긋나 한 판이 3일 만에 끝난 적이 있고(결함 BB-2), 코드의 기본값은 7이라
/// 코드만 읽어서는 드러나지 않았다.
///
/// ponytail: 기획서 4장이 `DT_DayPhase`로 관리한다고 정했다. 데이터 테이블이 생기면
/// 이 표를 그쪽으로 옮긴다.
public static class DayPhases
{
    /// 기획서 4장 표: 7일 × (밤 120 + 낮 120 + 전환 10) = 1,750초 ≈ 29분 10초.
    public const float NightSeconds = 120f;
    public const float DaySeconds = 120f;
    public const float TransitionSeconds = 10f;
    public const int TotalDays = 7;
}
