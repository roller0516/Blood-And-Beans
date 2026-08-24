using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// Client-only uGUI view of the replicated match state.
public class PhaseHud : MonoBehaviour
{
    [SerializeField] GamePhase phase;
    [SerializeField] Scoreboard board;
    [SerializeField] TransitionLedger ledger;

    Text label;
    float nextRefresh;

    void Awake()
    {
        var canvasObject = new GameObject("Match HUD", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;

        var textObject = new GameObject("Match State", typeof(RectTransform), typeof(Text));
        textObject.transform.SetParent(canvasObject.transform, false);
        label = textObject.GetComponent<Text>();
        label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        label.fontSize = 18;
        label.color = Color.white;
        label.alignment = TextAnchor.UpperLeft;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;

        var rect = label.rectTransform;
        rect.anchorMin = new Vector2(0f, 0f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.offsetMin = new Vector2(16f, 16f);
        rect.offsetMax = new Vector2(-16f, -16f);
    }

    void Update()
    {
        if (label == null || Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.1f;
        label.text = BuildText();
    }

    string BuildText()
    {
        if (phase == null || !phase.IsSpawned) return string.Empty;
        var text = new StringBuilder();
        text.AppendLine(phase.Finished
            ? "FINISHED"
            : $"Day {phase.Day} · {phase.Current} · {phase.Remaining:0.0}s");

        var player = LocalPlayer();
        var team = PlayerTeam.Local();
        var director = MatchDirector.Find();
        var cafe = director != null ? director.CafeOf(team) : null;

        if (phase.Current == Phase.Night && player != null)
        {
            var inventory = player.GetComponent<PlayerInventory>();
            if (inventory != null)
                text.AppendLine($"짐 {inventory.LoadRatio * 100f:0}% · 속도 {inventory.CurrentSpeedMultiplier * 100f:0}%");
        }
        else if (phase.Current == Phase.Transition && ledger != null)
        {
            text.AppendLine("내일의 손님");
            for (var race = 0; race < ledger.RaceCounts.Length; race++)
                if (ledger.RaceCounts[race] > 0) text.AppendLine($"{(Race)race} x{ledger.RaceCounts[race]}");
            text.AppendLine($"인기 재료: {string.Join(", ", ledger.PopularShown)}");
        }
        else if (phase.Current == Phase.Day && board != null)
        {
            var ranking = board.Ranking();
            for (var rank = 0; rank < ranking.Count; rank++)
            {
                var rankedTeam = ranking[rank];
                text.AppendLine($"{rank + 1}. Team {rankedTeam} · {board.RevenueOf(rankedTeam)}g" +
                    (rankedTeam == team ? " <" : ""));
            }
        }

        if (cafe?.Dishes != null)
            text.AppendLine($"접시 · 깨끗 {cafe.Dishes.Clean} / 사용 {cafe.Dishes.InUse} / 더러움 {cafe.Dishes.Dirty}");
        if (cafe?.Queue != null)
            foreach (var customer in cafe.Queue.Waiting)
                if (customer != null)
                    text.AppendLine($"{customer.Kind} · x{customer.Remaining} · 인내 {customer.PatienceRatio * 100f:0}%");

        var prompt = player?.GetComponent<PlayerInteractor>()?.Prompt;
        if (!string.IsNullOrEmpty(prompt)) text.AppendLine($"\n[F] {prompt}");
        return text.ToString();
    }

    static NetworkObject LocalPlayer()
    {
        var manager = NetworkManager.Singleton;
        return manager != null && manager.IsClient && manager.LocalClient != null
            ? manager.LocalClient.PlayerObject
            : null;
    }
}
