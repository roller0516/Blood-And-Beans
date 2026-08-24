using Unity.Netcode;
using UnityEngine;

public enum Judgement { Perfect, Good, Miss, Burnt }

/// The needle that sweeps after cooking finishes (doc 5.2). Lives on the same
/// GameObject as its Station.
///
/// The needle position is not synced — it is a pure function of server time, so
/// every client draws the same needle and the server can judge the exact instant
/// any client pressed F.
public class CompletionGauge : NetworkBehaviour
{
    [SerializeField] float window = 10f;        // completion stays hittable this long
    [SerializeField] float sweepsPerSecond = 1.4f;
    [SerializeField] float perfectHalfWidth = 0.05f;   // distance from centre 0.5
    [SerializeField] float goodHalfWidth = 0.16f;

    readonly NetworkVariable<bool> active = new();
    readonly NetworkVariable<double> startedAt = new();

    /// Server-side only. Station subscribes to learn how the bake turned out.
    public System.Action<Judgement> OnResult;

    // ponytail: one F stops the oldest live gauge *of your own cafe*, so two of your
    // machines finishing at once are resolved oldest-first. Add per-station targeting if
    // that reads badly in play.
    //
    // The candidate set is this cafe's own gauges. It used to be a static list holding
    // every cafe's, which is how a player in cafe A judged cafe B's oven whenever B's
    // gauge happened to be older (아키텍처_v1.0.md §1.2) — scoping the list removes the
    // chance to get it wrong rather than filtering an over-broad one.
    Cafe cafe;
    MatchDirector director;

    public bool Active => active.Value;

    public float Needle =>
        Mathf.PingPong((float)(NetworkManager.ServerTime.Time - startedAt.Value) * sweepsPerSecond, 1f);

    public float Remaining =>
        Mathf.Max(0f, window - (float)(NetworkManager.ServerTime.Time - startedAt.Value));

    public void BeginServer()
    {
        if (!IsServer || !IsDay) return;
        active.Value = true;
        startedAt.Value = NetworkManager.ServerTime.Time;
    }

    /// The owning cafe, not its team id: MatchDirector assigns team ids in its own Awake
    /// and the order between two Awakes is not something to depend on. The parent walk
    /// works regardless of who ran first.
    void Awake()
    {
        cafe = Cafe.Of(this);
        director = MatchDirector.Find();
    }

    int TeamId => cafe != null ? cafe.TeamId : -1;
    bool IsDay => director != null && director.Phase.Current == Phase.Day;

    void Update()
    {
        if (IsServer && IsDay && active.Value && Remaining <= 0f) Finish(Judgement.Burnt);

    }

    public static bool TryStopLocalClient()
    {
        foreach (var gauge in FindObjectsByType<CompletionGauge>(FindObjectsSortMode.None))
        {
            if (!gauge.IsDay || !gauge.active.Value || gauge.TeamId < 0 || gauge.TeamId != PlayerTeam.Local()) continue;
            if (gauge.OldestInThisCafe() != gauge) continue;
            gauge.StopRpc();
            return true;
        }
        return false;
    }

    CompletionGauge OldestInThisCafe()
    {
        if (cafe == null) return null;

        CompletionGauge best = null;
        foreach (var g in cafe.Gauges)
        {
            if (g == null || !g.active.Value) continue;
            if (best == null || g.startedAt.Value < best.startedAt.Value) best = g;
        }
        return best;
    }

    /// An `[Rpc(SendTo.Server)]` is callable by any client, so the team check has to be
    /// here and not only in the caller's Update.
    [Rpc(SendTo.Server)]
    public void StopRpc(RpcParams p = default)
    {
        if (!IsDay || !active.Value || TeamId < 0) return;
        if (PlayerTeam.Of(p.Receive.SenderClientId) != TeamId) return;
        Finish(Judge(Needle));
    }

    void Finish(Judgement j)
    {
        active.Value = false;
        OnResult?.Invoke(j);
    }

    Judgement Judge(float pos)
    {
        var off = Mathf.Abs(pos - 0.5f);
        return off <= perfectHalfWidth ? Judgement.Perfect
             : off <= goodHalfWidth ? Judgement.Good
             : Judgement.Miss;
    }

    /// Doc 5.6.2. Burnt keeps its 0.3 here so the sale path has one number to read.
    public static float MultiplierOf(Judgement j) => j switch
    {
        Judgement.Perfect => 1.3f,
        Judgement.Good => 1.0f,
        Judgement.Miss => 0.7f,
        _ => 0.3f,
    };
}
