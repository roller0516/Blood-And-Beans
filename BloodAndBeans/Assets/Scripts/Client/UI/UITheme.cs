using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 목업(`00__Docs/02__Art/02__UI/UI_목업.pptx`)의 색과 조립 도구.
///
/// 목업은 1920×1080에 좌상단 원점으로 그려져 있고, 이 프로젝트의 CanvasScaler 기준
/// 해상도도 1920×1080이다. 그래서 목업의 좌표를 그대로 넣을 수 있게 `At`가 좌상단
/// 기준으로 앵커와 피벗을 잡는다 — 화면마다 y를 뒤집는 산수를 반복하지 않기 위해서다.
///
/// **좌표가 절대값이라 캔버스가 1920×1080보다 작아지면 그만큼 잘려 나간다.** 실제로
/// 21:9에서 확정 버튼이 화면 밖으로 밀려나 진행이 막혔다. 그래서 위젯을 캔버스에 직접
/// 붙이지 않고 `Stage`가 세운 고정 1920×1080 판 위에 올린다. CanvasScaler를 `Expand`로
/// 두면 캔버스 논리 크기가 어느 축에서도 기준 해상도 아래로 내려가지 않으므로 그 판이
/// 통째로 들어온다. 남는 공간은 여백이 되고, 배경만 캔버스 전체를 덮는다.
///
/// ponytail: `MatchHudScreen`에도 비슷한 private 헬퍼가 있지만 합치지 않았다. 그 파일은
/// 지금 다른 작업이 올라가 있어 건드리면 충돌한다. 그쪽이 정리되면 이 클래스로 모은다.
public static class UITheme
{
    static UIThemeConfig config;

    /// 기본 디자인 원본. `Resources`에 애셋이 없으면 코드 기본값으로 만든 인스턴스를 쓴다 —
    /// 그 기본값은 애셋이 생기기 전과 같은 값이라 화면이 깨지지는 않지만, 개발 콘솔에서
    /// 만진 값이 저장되지 않으므로 한 번 경고한다.
    public static UIThemeConfig Config
    {
        get
        {
            if (config != null) return config;

            config = Resources.Load<UIThemeConfig>(UIThemeConfig.AssetName);
            if (config == null)
            {
                config = ScriptableObject.CreateInstance<UIThemeConfig>();
                CDebug.LogWarning(
                    $"Resources/{UIThemeConfig.AssetName} 애셋이 없다. 코드 기본값으로 그린다. " +
                    "개발 콘솔의 'UI 테마'에서 만들 수 있다.");
            }
            return config;
        }
    }

    /// 개발 콘솔이 애셋을 새로 만들거나 고른 뒤 부른다. 다음 조회부터 그 애셋을 읽는다.
    public static void UseConfig(UIThemeConfig replacement) => config = replacement;

    /// 목업이 그려진 판의 크기. 이 값과 CanvasScaler 기준 해상도가 같아야 좌표가 1:1이다.
    public static float StageWidth => Config.StageSize.x;
    public static float StageHeight => Config.StageSize.y;

    /// 화면 루트에 배경과 무대를 세우고 무대를 돌려준다. 이후 위젯은 전부 무대에 붙인다.
    ///
    /// 배경은 캔버스 전체로 늘린다 — 무대만 칠하면 여백으로 게임 월드가 비친다.
    public static RectTransform Stage(Transform root, Color background)
    {
        var back = new GameObject("Backdrop", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var backRect = (RectTransform)back.transform;
        backRect.SetParent(root, false);
        backRect.anchorMin = Vector2.zero;
        backRect.anchorMax = Vector2.one;
        backRect.pivot = new Vector2(0.5f, 0.5f);
        backRect.offsetMin = backRect.offsetMax = Vector2.zero;

        var image = back.GetComponent<UnityEngine.UI.Image>();
        image.color = background;
        // 전체 화면 UI의 배경이므로 클릭을 여기서 멈춘다. 뒤의 화면으로 새어 나가면 안 된다.
        image.raycastTarget = true;

        var stage = new GameObject("Stage", typeof(RectTransform));
        var rect = (RectTransform)stage.transform;
        rect.SetParent(root, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(StageWidth, StageHeight);
        rect.anchoredPosition = Vector2.zero;
        return rect;
    }
    public static Color Ink       => Config.Ink;       // 배경
    public static Color Panel     => Config.Panel;     // 패널 바닥
    public static Color PanelDeep => Config.PanelDeep; // 카드 안쪽
    public static Color Cream     => Config.Cream;     // 본문 글자
    public static Color Gold      => Config.Gold;      // 구분선·라벨
    public static Color GoldLit   => Config.GoldLit;   // 강조 수치
    public static Color Green     => Config.Green;     // 이득
    public static Color Red       => Config.Red;       // 손실·카운트다운
    public static Color Blue      => Config.Blue;      // 밤
    public static Color Purple    => Config.Purple;    // 업그레이드 재료
    public static Color Ice       => Config.Ice;       // 슬롯·아이콘 자리

    /// 에셋이 아직 없는 아이콘·초상·썸네일 자리. 목업의 베이지 사각형이 이것이다.
    public static Color Placeholder => Config.Placeholder;

    /// 플레이어가 고르는 팀 색. 목업 2번의 `MY NAMEPLATE` 팔레트 순서 그대로이며,
    /// 인게임 네임플레이트에도 같은 색이 쓰인다.
    ///
    /// ponytail: 기획서에 팀 색 개념이 없다(9장·10장 어디에도). 목업에서만 온 값이라
    /// 테마 애셋에 두고, 확정되면 팀 데이터 원본으로 옮긴다.
    public static Color[] TeamColors => Config.TeamColors;

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

    /// 목업의 얇은 구분선. 두께는 테마가 정한다.
    public static RectTransform Rule(Transform parent, Color color, float x, float y, float w)
        => Box(parent, "Rule", color, x, y, w, Config.RuleHeight);

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
        => Text(parent, "Caption", value, Config.CaptionSize, Gold, x, y, w, Config.CaptionHeight);

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

        var labelHeight = Config.ButtonLabelHeight;
        Text(go.transform, "Label", label,
             primary ? Config.ButtonPrimaryLabelSize : Config.ButtonSecondaryLabelSize,
             primary ? Ink : Cream, 0f, (h - labelHeight) * 0.5f, w, labelHeight,
             TextAlignmentOptions.Top);

        return go.GetComponent<UnityEngine.UI.Button>();
    }
}
