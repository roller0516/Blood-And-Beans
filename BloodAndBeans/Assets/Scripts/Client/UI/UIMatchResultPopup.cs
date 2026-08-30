using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// 판이 끝났을 때 뜨는 최종 결산 (기획서 3.1). 마지막 낮이 끝나면 누적 판매 매출이 가장
/// 높은 팀이 승리한다.
///
/// 매출만 보여 준다. 기획서 3.1이 보유 재료·설비·캐릭터는 비공개라고 못박았고, 애초에
/// `Scoreboard`가 복제하는 것도 매출뿐이다.
///
/// 위젯을 코드로 세우는 것은 `UIReturnResultPopup`과 같은 이유다. 자리와 크기가 서로 물려
/// 있어서 Inspector에 흩어 두면 한 칸만 어긋나도 배치가 무너진다. 프리팹에는 Canvas와
/// 이 스크립트만 있으면 된다.
public sealed class UIMatchResultPopup : UIPopup
{
    [Header("색")]
    [SerializeField] Color win = new(0.95f, 0.84f, 0.42f);
    [SerializeField] Color draw = new(0.72f, 0.78f, 0.88f);
    [SerializeField] Color lose = new(0.78f, 0.55f, 0.52f);
    [SerializeField] Color muted = new(0.72f, 0.72f, 0.68f, 0.9f);
    [SerializeField] Color panelBack = new(0.04f, 0.05f, 0.05f, 0.94f);

    [Header("크기")]
    [SerializeField] Vector2 panelSize = new(520f, 360f);

    TMP_Text titleText;
    TMP_Text lineText;
    TMP_Text standingsText;

    readonly StringBuilder text = new();

    /// 판이 끝났으므로 조작할 것이 없다. 커서를 돌려주고 입력을 막는다.
    public override bool BlocksPlayerInput => true;

    protected override void Awake()
    {
        base.Awake();
        Build();
    }

    /// `revenueByTeam`은 `Scoreboard`가 복제한 값 그대로다. 승패 판정은 여기서 하지 않고
    /// `FinalStandings`에 묻는다 — 씬 없이 기획서와 대조할 수 있어야 하는 규칙이다.
    public void Bind(IReadOnlyList<int> revenueByTeam, int myTeam)
    {
        var winners = FinalStandings.WinnersOf(revenueByTeam);
        var tie = FinalStandings.IsTie(revenueByTeam);
        var iWon = winners.Contains(myTeam);

        if (tie && iWon)
        {
            titleText.text = "무승부";
            titleText.color = draw;
            lineText.text = $"공동 1위 · {Join(winners)}";
            lineText.color = draw;
        }
        else if (iWon)
        {
            titleText.text = "승리";
            titleText.color = win;
            lineText.text = "7일간의 누적 매출 1위";
            lineText.color = win;
        }
        else
        {
            titleText.text = "패배";
            titleText.color = lose;
            lineText.text = winners.Count > 0 ? $"1위 · {Join(winners)}" : "판정할 매출이 없다";
            lineText.color = lose;
        }

        standingsText.text = Standings(revenueByTeam, myTeam);
    }

    /// 매출 내림차순. 같은 매출이면 같은 등수를 준다 — 1위 판정과 어긋나면 안 된다.
    string Standings(IReadOnlyList<int> revenueByTeam, int myTeam)
    {
        text.Clear();
        if (revenueByTeam == null || revenueByTeam.Count == 0) return string.Empty;

        var order = new List<int>(revenueByTeam.Count);
        for (var team = 0; team < revenueByTeam.Count; team++) order.Add(team);
        order.Sort((a, b) => revenueByTeam[b].CompareTo(revenueByTeam[a]));

        var rank = 0;
        for (var i = 0; i < order.Count; i++)
        {
            var team = order[i];
            if (i == 0 || revenueByTeam[team] != revenueByTeam[order[i - 1]]) rank = i + 1;

            text.Append(rank).Append(". Team ").Append(team)
                .Append("  ").Append(revenueByTeam[team].ToString("N0")).Append('G');
            if (team == myTeam) text.Append("  <");
            text.AppendLine();
        }
        return text.ToString();
    }

    static string Join(List<int> teams)
    {
        var parts = new string[teams.Count];
        for (var i = 0; i < teams.Count; i++) parts[i] = $"Team {teams[i]}";
        return string.Join(", ", parts);
    }

    void Build()
    {
        var panel = new GameObject("Panel", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        var rect = (RectTransform)panel.transform;
        rect.SetParent(transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = panelSize;
        rect.anchoredPosition = Vector2.zero;

        var back = panel.GetComponent<UnityEngine.UI.Image>();
        back.color = panelBack;
        back.raycastTarget = false;

        titleText = MakeText(rect, "Title", new Vector2(0.5f, 1f), new Vector2(0f, -34f),
                             new Vector2(panelSize.x - 40f, 52f), 40f, win,
                             TextAlignmentOptions.Center);
        lineText = MakeText(rect, "Line", new Vector2(0.5f, 1f), new Vector2(0f, -88f),
                            new Vector2(panelSize.x - 40f, 34f), 22f, muted,
                            TextAlignmentOptions.Center);

        // 순위는 여러 줄이라 가운데 정렬하면 자릿수마다 들쭉날쭉해진다. 왼쪽에 붙인다.
        standingsText = MakeText(rect, "Standings", new Vector2(0.5f, 1f), new Vector2(0f, -130f),
                                 new Vector2(panelSize.x - 80f, 180f), 20f, muted,
                                 TextAlignmentOptions.TopLeft);
    }

    static TMP_Text MakeText(Transform parent, string name, Vector2 anchor, Vector2 position,
                             Vector2 size, float fontSize, Color color,
                             TextAlignmentOptions alignment)
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
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }
}
