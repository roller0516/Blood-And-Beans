using Unity.Netcode;
using UnityEngine;

public enum Phase { Night, Transition, Day }

/// Server-authoritative day loop: Night -> Transition -> Day, repeating for totalDays.
/// Clients derive the countdown from the synced end time, so nothing ticks over the wire.
public class GamePhase : NetworkBehaviour
{
    [SerializeField] float nightSeconds = 120f;
    [SerializeField] float transitionSeconds = 10f;
    [SerializeField] float daySeconds = 120f;
    [SerializeField] int totalDays = 3;

    readonly NetworkVariable<Phase> phase = new();
    readonly NetworkVariable<int> day = new();
    readonly NetworkVariable<double> endsAt = new();
    readonly NetworkVariable<bool> finished = new();

    public Phase Current => phase.Value;
    public int Day => day.Value;
    public bool Finished => finished.Value;
    public float Remaining =>
        Mathf.Max(0f, (float)(endsAt.Value - NetworkManager.ServerTime.Time));
    public float Elapsed => Mathf.Max(0f, Duration(Current) - Remaining);

    /// Pure transition rule, kept separate so the order is readable in one line.
    /// Fired on the server the moment a phase begins. Systems that must react to a
    /// boundary subscribe here instead of each polling for their own edge — two
    /// components watching the same transition independently is how the fog reset
    /// silently stopped happening.
    ///
    /// An instance event, not a static one: a static event keeps handlers from objects
    /// that a domain reload or a finished match already destroyed, and those fire into
    /// despawned NetworkBehaviours (아키텍처_v1.0.md §1.5). Subscribers reach it through
    /// MatchDirector.Phase, which they resolve once at spawn.
    public event System.Action<Phase> PhaseEntered;

    public static Phase NextPhase(Phase p) => p switch
    {
        Phase.Night => Phase.Transition,
        Phase.Transition => Phase.Day,
        _ => Phase.Night,
    };

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;
        day.Value = 1;
        finished.Value = false;   // a respawned loop must not stay permanently finished
        Enter(Phase.Night);
    }

    void Update()
    {
        if (!IsServer || finished.Value) return;
        if (NetworkManager.ServerTime.Time < endsAt.Value) return;

        // A finished Day rolls the calendar; the last one ends the run.
        if (phase.Value == Phase.Day)
        {
            if (day.Value >= totalDays) { finished.Value = true; return; }
            day.Value++;
        }
        Enter(NextPhase(phase.Value));
    }

    void Enter(Phase p)
    {
        phase.Value = p;
        endsAt.Value = NetworkManager.ServerTime.Time + Duration(p);
        PhaseEntered?.Invoke(p);
    }

    float Duration(Phase p) => p switch
    {
        Phase.Night => nightSeconds,
        Phase.Transition => transitionSeconds,
        _ => daySeconds,
    };
}
