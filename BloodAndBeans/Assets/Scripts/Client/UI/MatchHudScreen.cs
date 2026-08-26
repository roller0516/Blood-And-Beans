using UnityEngine;
using UnityEngine.UI;

/// 매치 중 상시 떠 있는 HUD. 날짜·페이즈·남은 시간·팀·카페 상태를 보여 준다.
///
/// 화면 오른쪽에 붙는다. 왼쪽 위는 개발용 HUD(`NetworkHud`, `CheatHud`)가 쓰는 열이라
/// 여기까지 왼쪽에 그리면 글자가 겹친다.
///
/// 이 클래스는 값을 만들지 않는다. 무엇을 쓸지는 `MatchHudPresenter`가 정한다 —
/// 예전에는 한 클래스가 캔버스를 만들고, 복제 상태를 읽고, 문자열을 조립하고, 로컬
/// 플레이어 컴포넌트까지 캐시했다.
public sealed class MatchHudScreen : UIScreen
{
    [SerializeField] Text label;

    public void Render(string value)
    {
        if (label != null) label.text = value;
    }
}
