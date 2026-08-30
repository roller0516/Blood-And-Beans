using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 밤이 끝난 직후 자기 귀환 결과를 알려 주는 팝업 (기획서 6.8).
///
/// 문구는 기획서 6.8의 세 갈래를 그대로 쓴다. 여기서 말을 새로 지어내면 기획서와
/// 구현이 서로 다른 규칙을 설명하게 된다.
///
/// | 조건 | 문구 |
/// |---|---|
/// | 소환 위치 O + 가방 소지 O | 획득 아이템 100% 귀환 |
/// | 소환 위치 X + 가방 소지 O | 획득 아이템 n% 소실 |
/// | 가방 소지 X (위치 무관)   | 획득 아이템 100% 소실 |
///
/// 위젯을 코드로 세우는 것은 `MatchHudScreen`과 같은 이유다. 자리와 크기가 서로 물려
/// 있어서 Inspector에 흩어 두면 한 칸만 어긋나도 배치가 무너진다. 프리팹에는 Canvas와
/// 이 스크립트만 있으면 된다.
public sealed class UIReturnResultPopup : UIPopup
{
    [Header("색")]
    [SerializeField] Color success = new(0.55f, 0.85f, 0.52f);
    [SerializeField] Color warning = new(0.95f, 0.76f, 0.33f);
    [SerializeField] Color failure = new(0.93f, 0.35f, 0.28f);
    [SerializeField] Color muted = new(0.72f, 0.72f, 0.68f, 0.9f);
    [SerializeField] Color panelBack = new(0.04f, 0.05f, 0.05f, 0.92f);

    [Header("크기")]
    [SerializeField] Vector2 panelSize = new(460f, 240f);

    TMP_Text titleText;
    TMP_Text lineText;
    TMP_Text detailText;

    /// 전환은 10초뿐이고 그동안 플레이어가 할 일이 없다. 창을 띄운 채 두면 정보가
    /// 남아 있는 편이 낫고, 닫는 것은 `MatchFlow`가 낮이 시작될 때 한다.
    public override bool BlocksPlayerInput => false;

    protected override void Awake()
    {
        base.Awake();
        Build();
    }

    /// 결과를 그린다. `n%`는 `ReturnZone`이 들고 있는 실제 설정값에서 온다 — 문구에
    /// 50을 박아 두면 인스펙터에서 비율을 바꿨을 때 화면만 거짓말을 한다.
    public void Bind(ReturnOutcome outcome, int kept, int lost, int lossPercent)
    {
        switch (outcome)
        {
            case ReturnOutcome.Returned:
                titleText.text = "귀환 성공";
                titleText.color = success;
                lineText.text = "획득 아이템 100% 귀환";
                lineText.color = success;
                detailText.text = kept > 0
                    ? $"재료 {kept}개를 팀 재고로 넘겼다"
                    : "가져온 재료가 없다";
                break;

            case ReturnOutcome.PartialLoss:
                titleText.text = "귀환 실패";
                titleText.color = warning;
                lineText.text = $"획득 아이템 {lossPercent}% 소실";
                lineText.color = warning;
                detailText.text = $"소환 위치 밖에서 밤이 끝났다 · {lost}개 소실 / {kept}개 반입";
                break;

            default:
                titleText.text = "가방 분실";
                titleText.color = failure;
                lineText.text = "획득 아이템 100% 소실";
                lineText.color = failure;
                detailText.text = lost > 0
                    ? $"묻어 둔 가방을 회수하지 못했다 · {lost}개 소실"
                    : "묻어 둔 가방을 회수하지 못했다";
                break;
        }
    }

    void Build()
    {
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        var rect = (RectTransform)panel.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = panelSize;
        rect.anchoredPosition = Vector2.zero;

        var back = panel.GetComponent<Image>();
        back.color = panelBack;
        back.raycastTarget = false;

        titleText = MakeText(rect, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -28f),
                             new Vector2(panelSize.x - 40f, 44f), 34f, success);
        lineText = MakeText(rect, "Line", new Vector2(0.5f, 0.5f), new Vector2(0f, 4f),
                            new Vector2(panelSize.x - 40f, 36f), 24f, success);
        detailText = MakeText(rect, "Detail", new Vector2(0.5f, 0f), new Vector2(0f, 34f),
                              new Vector2(panelSize.x - 40f, 28f), 16f, muted);
    }

    static TMP_Text MakeText(Transform parent, string name, Vector2 anchor, Vector2 position,
                             Vector2 size, float fontSize, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = anchor;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;

        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
        return text;
    }
}
