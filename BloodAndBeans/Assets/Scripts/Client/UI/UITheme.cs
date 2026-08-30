using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 목업(`00__Docs/02__Art/02__UI/UI_목업.pptx`)의 색과 조립 도구.
///
/// 목업은 1920×1080에 좌상단 원점으로 그려져 있고, 이 프로젝트의 CanvasScaler 기준
/// 해상도도 1920×1080이다. 그래서 목업의 좌표를 그대로 넣을 수 있게 `At`가 좌상단
/// 기준으로 앵커와 피벗을 잡는다 — 화면마다 y를 뒤집는 산수를 반복하지 않기 위해서다.
///
/// ponytail: `MatchHudScreen`에도 비슷한 private 헬퍼가 있지만 합치지 않았다. 그 파일은
/// 지금 다른 작업이 올라가 있어 건드리면 충돌한다. 그쪽이 정리되면 이 클래스로 모은다.
public static class UITheme
{
    public static readonly Color Ink       = Hex(0x120C08); // 배경
    public static readonly Color Panel     = Hex(0x0E0905); // 패널 바닥
    public static readonly Color PanelDeep = Hex(0x180F09); // 카드 안쪽
    public static readonly Color Cream     = Hex(0xF2E3CB); // 본문 글자
    public static readonly Color Gold      = Hex(0xC6974A); // 구분선·라벨
    public static readonly Color GoldLit   = Hex(0xE9B85C); // 강조 수치
    public static readonly Color Green     = Hex(0x7CD9A8); // 이득
    public static readonly Color Red       = Hex(0xD9563F); // 손실·카운트다운
    public static readonly Color Blue      = Hex(0x7CAFD9); // 밤
    public static readonly Color Purple    = Hex(0xA46EE8); // 업그레이드 재료
    public static readonly Color Ice       = Hex(0xE6EEF5); // 슬롯·아이콘 자리

    /// 에셋이 아직 없는 아이콘·초상·썸네일 자리. 목업의 베이지 사각형이 이것이다.
    public static readonly Color Placeholder = Hex(0xF2E3CB);

    static Color Hex(int rgb) => new(
        ((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);

    /// 좌상단 원점 배치. 목업에서 읽은 (x, y, w, h)를 그대로 넘긴다.
    public static RectTransform At(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(w, h);
        rect.anchoredPosition = new Vector2(x, -y);
        return rect;
    }

    public static RectTransform Box(Transform parent, string name, Color color,
                                    float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image));
        go.transform.SetParent(parent, false);
        var image = go.GetComponent<UnityEngine.UI.Image>();
        image.color = color;
        image.raycastTarget = false;
        return At((RectTransform)go.transform, x, y, w, h);
    }

    /// 목업의 1px 구분선.
    public static RectTransform Rule(Transform parent, Color color, float x, float y, float w)
        => Box(parent, "Rule", color, x, y, w, 1f);

    public static TMP_Text Text(Transform parent, string name, string value, float size,
                                Color color, float x, float y, float w, float h,
                                TextAlignmentOptions align = TextAlignmentOptions.TopLeft)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        At((RectTransform)go.transform, x, y, w, h);

        var text = go.GetComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = align;
        text.raycastTarget = false;
        return text;
    }

    /// 목업의 대문자 소제목 (`TODAY'S TRADE`, `STANDINGS` …).
    public static TMP_Text Caption(Transform parent, string value, float x, float y, float w)
        => Text(parent, "Caption", value, 11f, Gold, x, y, w, 16f);

    /// 왼쪽에서 자라는 막대. 반환값의 `localScale.x`가 진행도다 — 폭을 만지면 자식
    /// 텍스트까지 늘어나므로 스케일로 준다.
    public static RectTransform Bar(Transform parent, Color back, Color fill,
                                    float x, float y, float w, float h)
    {
        var backRect = Box(parent, "Bar", back, x, y, w, h);

        var go = new GameObject("Fill", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var rect = (RectTransform)go.transform;
        rect.SetParent(backRect, false);
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0f, 0.5f);
        rect.offsetMin = rect.offsetMax = Vector2.zero;

        var image = go.GetComponent<UnityEngine.UI.Image>();
        image.color = fill;
        image.raycastTarget = false;
        return rect;
    }

    /// 목업의 금색 확정 버튼과 테두리 없는 보조 버튼.
    public static Button Button(Transform parent, string name, string label, bool primary,
                               float x, float y, float w, float h)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image),
                                typeof(UnityEngine.UI.Button));
        go.transform.SetParent(parent, false);
        At((RectTransform)go.transform, x, y, w, h);

        var image = go.GetComponent<UnityEngine.UI.Image>();
        image.color = primary ? Gold : Panel;

        Text(go.transform, "Label", label, primary ? 20f : 16f,
             primary ? Ink : Cream, 0f, (h - 26f) * 0.5f, w, 26f,
             TextAlignmentOptions.Top);

        return go.GetComponent<UnityEngine.UI.Button>();
    }
}
