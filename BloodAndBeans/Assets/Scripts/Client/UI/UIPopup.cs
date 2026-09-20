using DG.Tweening;
using UnityEngine;

/// 화면 위에 겹쳐 뜨는 창. 확인창·입력창·설정처럼 밑의 화면을 지우지 않는 것들이다.
/// 화면과 스택을 나눠 두는 이유는 팝업이 닫혀도 밑의 화면이 그대로여야 하기 때문이다.
///
/// ponytail: 밑의 입력을 막는 모달 처리는 넣지 않았다. 그런 팝업이 실제로 생기면 프리팹
/// 루트에 전면 Image(raycastTarget)를 한 장 깔면 된다. 프레임워크가 할 일이 아니다.
public abstract class UIPopup : UIView
{
    /// 열릴 때 재생할 연출. 스케일 펀치든 페이드든 인스펙터에서 정하고, 연출이 없는
    /// 팝업은 비워 둔다 — 팝업마다 다르므로 코드가 정할 값이 아니다.
    ///
    /// **Canvas가 붙은 루트에 걸지 않는다.** Screen Space Overlay Canvas는 자기
    /// RectTransform의 위치·스케일을 매 프레임 되돌린다. 안쪽 패널에 붙인다.
    [SerializeField] DOTweenAnimation showAnimation;

    public override void OnShow()
    {
        base.OnShow();
        // AutoPlay는 처음 한 번만 돈다. 팝업은 감췄다 다시 뜨므로 여기서 다시 만들어 돌린다.
        if (showAnimation != null) showAnimation.RewindThenRecreateTweenAndPlay();
    }

    public override void OnHide()
    {
        base.OnHide();
        // 되감지 않고 끄면 연출 중간값이 그대로 남아, 다음에 열 때 그 값에서 시작한다.
        if (showAnimation != null) showAnimation.DORewind();
    }
}
