using UnityEngine;
using UnityEngine.UI;

/// 프리팹 하나로 존재하는 UI 화면의 공통 뼈대. `UIManager`가 만들고 보여 주고 감춘다.
///
/// 프리팹 루트에 Canvas를 두는 이유는 겹침 순서 때문이다. 예전에는 화면마다 자기
/// `sortingOrder`를 코드에 박아 뒀고(개발 HUD 20, 매치 HUD 10, 타이틀 30) 그래서 새 화면이
/// 생길 때마다 누가 누구를 덮는지가 우연에 맡겨졌다. 이제 순서는 `UIManager`가 정한다.
[RequireComponent(typeof(Canvas), typeof(GraphicRaycaster))]
public abstract class UIView : MonoBehaviour
{
    Canvas canvas;

    /// 이 뷰가 지금 스택에 올라가 있는가. 표시 여부와 다르다 — 화면 스택에서 밑에 깔린
    /// 화면은 스택에 있지만 보이지는 않는다.
    public bool IsOnStack { get; internal set; }

    protected virtual void Awake()
    {
        canvas = GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
    }

    /// 겹침 순서. `UIManager`만 부른다.
    internal void SetSortingOrder(int order)
    {
        if (canvas == null) canvas = GetComponent<Canvas>();
        canvas.overrideSorting = true;
        canvas.sortingOrder = order;
    }

    internal void SetVisible(bool value)
    {
        if (gameObject.activeSelf == value) return;
        gameObject.SetActive(value);
    }

    bool shown;

    /// `UIManager` 전용. 같은 상태로 두 번 부르면 구독이 겹치거나 해제가 두 번 돈다.
    internal void ShowInternal()
    {
        if (shown) return;
        shown = true;
        OnShow();
    }

    internal void HideInternal()
    {
        if (!shown) return;
        shown = false;
        OnHide();
    }

    /// 이 뷰가 떠 있는 동안 마우스 커서가 필요한가. 버튼을 누르는 화면·팝업은 그렇고,
    /// 정보만 보여 주는 HUD는 아니다. 커서를 실제로 켜고 끄는 것은 `UIManager`다 —
    /// 뷰마다 만지면 두 뷰가 겹쳤을 때 나중에 사라지는 쪽이 커서를 가져간다.
    public virtual bool WantsCursor => true;

    /// 이 뷰가 떠 있는 동안 캐릭터 조작을 통째로 막는가. 이동·대시·상호작용·쏟기·가방
    /// 묻기가 함께 죽는다. 설정처럼 게임을 잠시 접어 두는 창이 그렇다.
    ///
    /// 상자 루팅 창은 아니다 — 기획서 6.5.5가 "이동 키를 누르면 상자 UI가 즉시 닫힘"으로
    /// 정했으므로 이동 입력이 죽으면 그 규칙이 발동할 방법이 없다.
    ///
    /// 회전은 여기서 정하지 않는다. 커서를 푸는 창은 예외 없이 마우스를 UI에 넘긴 것이라
    /// `WantsCursor` 하나로 갈린다 (`LookSensitivity`).
    public virtual bool BlocksPlayerInput => false;

    /// 스택에 올라와 화면에 나타날 때. 구독은 여기서 시작한다.
    public virtual void OnShow() { }

    /// 가려지거나 스택에서 내려갈 때. `OnShow`에서 건 구독을 반드시 여기서 푼다.
    public virtual void OnHide() { }
}
