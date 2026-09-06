using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// 개발용 HUD(`NetworkHud`, `CheatHud`)가 공유하는 uGUI 조립 도우미.
/// 빌드에 나갈 UI가 아니라서 씬에 프리팹으로 두지 않고 런타임에 만든다.
///
/// 폰트·행 높이·색이 한 곳에만 있어야 두 HUD가 같은 열에 붙어도 줄이 어긋나지 않는다.
public static class DevHud
{
    const int FontSize = 16;
    const float RowHeight = 28f;
    const float ButtonHeight = 32f;
    static readonly Color ButtonColor = new(0.18f, 0.20f, 0.24f, 0.95f);
    /// 패널 배경. 밝은 지형 위에서도 글자가 읽혀야 한다.
    static readonly Color PanelColor = new(0f, 0f, 0f, 0.55f);
    static readonly Color DisabledColor = new(0.12f, 0.13f, 0.15f, 0.95f);
    
    /// 화면 좌상단 기준으로 세로 목록 패널 하나를 만든다. 반환값의 자식으로 행을 붙인다.
    public static RectTransform MakePanel(Transform parent, string name, int sortingOrder,
                                          Vector2 anchoredPosition, Vector2 size)
    {
        var canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(parent, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        var panel = new GameObject("Rows", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(canvasObject.transform, false);

        var backdrop = panel.GetComponent<Image>();
        backdrop.color = PanelColor;
        backdrop.raycastTarget = false;   // 배경이 버튼 클릭을 가로채면 안 된다
        var rect = (RectTransform)panel.transform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return rect;
    }

    /// 폰트는 지정하지 않는다. TMP가 `TMP_Settings.defaultFontAsset`을 쓴다 — 개발용 HUD가
    /// 자기 폰트 애셋을 들고 다닐 이유는 없고, 프로젝트 기본값이 바뀌면 같이 따라가야 한다.
    public static TMP_Text MakeText(Transform parent, string value)
    {
        var gameObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        gameObject.transform.SetParent(parent, false);
        var text = gameObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = FontSize;
        text.color = Color.white;
        text.text = value;
        gameObject.GetComponent<LayoutElement>().preferredHeight = RowHeight;
        return text;
    }

    public static Button MakeButton(Transform parent, string label, UnityAction action)
    {
        var gameObject = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        gameObject.transform.SetParent(parent, false);
        var background = gameObject.GetComponent<Image>();
        background.color = ButtonColor;
        gameObject.GetComponent<LayoutElement>().preferredHeight = ButtonHeight;
        var button = gameObject.GetComponent<Button>();
        // 런타임 AddComponent는 Reset을 부르지 않아 targetGraphic이 비어 있다. 직접 채운다.
        button.targetGraphic = background;
        button.onClick.AddListener(action);

        var text = MakeText(gameObject.transform, label);
        text.alignment = TextAlignmentOptions.Center;
        var rect = text.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        return button;
    }

    /// 버튼을 잠글 때 색까지 같이 바꾼다. `interactable`만 끄면 화면상 구분이 안 된다.
    public static void SetInteractable(Button button, bool value)
    {
        if (button == null || button.interactable == value) return;
        button.interactable = value;
        if (button.targetGraphic != null) button.targetGraphic.color = value ? ButtonColor : DisabledColor;
    }
}
