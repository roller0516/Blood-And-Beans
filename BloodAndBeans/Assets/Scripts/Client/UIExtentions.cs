using UnityEditor.Graphs;
using UnityEngine;
using UnityEngine.UI;

public static class UIExtentions
{
    //static readonly Color ButtonColor = new(0.18f, 0.20f, 0.24f, 0.95f);
    //static readonly Color DisabledColor = new(0.12f, 0.13f, 0.15f, 0.95f);
    
    /// 버튼을 잠글 때 색까지 같이 바꾼다. `interactable`만 끄면 화면상 구분이 안 된다.
    public static void SetInteractable(this Button button, bool value)
    {
        if (button == null || button.interactable == value) return;
        button.interactable = value;
    }
}
