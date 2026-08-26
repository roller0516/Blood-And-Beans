using System;
using UnityEngine.UI;

/// 화면들이 공유하는 버튼 배선 도우미.
///
/// 항상 `RemoveAllListeners`를 먼저 하는 이유는 화면이 재사용되기 때문이다. `UIManager`는
/// 한 번 만든 화면을 감췄다 다시 보여 주므로, 지우지 않고 걸면 두 번째 진입부터 콜백이
/// 두 번씩 돈다.
public static class UIButtons
{
    public static void Wire(Button button, Action action)
    {
        if (button == null) return;
        button.onClick.RemoveAllListeners();
        if (action != null) button.onClick.AddListener(() => action());
    }
}
