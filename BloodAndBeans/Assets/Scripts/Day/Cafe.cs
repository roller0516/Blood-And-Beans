using UnityEngine;

/// One team's cafe. Everything a station needs to find lives here, so the day systems
/// stop reaching for static singletons — with two teams there are two of each.
public class Cafe : MonoBehaviour
{
    /// Not serialized any more. `MatchDirector` decides which slot is which team and
    /// tells this cafe, so there is exactly one place where team ids come from
    /// (아키텍처_v1.0.md §1.4). Duplicating a cafe used to duplicate a stale id with it.
    int teamId = -1;

    public int TeamId => teamId;

    public Dish Dishes { get; private set; }
    public CustomerQueue Queue { get; private set; }
    public TeamStock Stock { get; private set; }

    /// This cafe's gauges, cached. Replaces a static list that held every cafe's gauges
    /// at once — the reason one team could stop another's oven (아키텍처_v1.0.md §1.2).
    public CompletionGauge[] Gauges { get; private set; } = new CompletionGauge[0];

    void Awake()
    {
        Dishes = GetComponentInChildren<Dish>(true);
        Queue = GetComponentInChildren<CustomerQueue>(true);
        Stock = GetComponentInChildren<TeamStock>(true);
        Gauges = GetComponentsInChildren<CompletionGauge>(true);
    }

    /// Called by MatchDirector on every peer during Awake.
    public void AssignTeam(int team) => teamId = team;

    /// Every fixture is parented under its cafe, so ownership is just a walk upwards.
    public static Cafe Of(Component c) => c == null ? null : c.GetComponentInParent<Cafe>();

    /// Reach is not authority. The cafes are separated by nothing but distance, so every
    /// day-side server entry point has to ask whose kitchen this is — otherwise a player
    /// can walk into a rival's cafe and empty their larder or serve from their counter.
    /// Same defect class as the completion gauge (아키텍처_v1.0.md §1.2).
    ///
    /// An unassigned cafe (teamId -1) matches nobody, which is the safe direction: a
    /// missing roster locks the kitchen rather than opening it to everyone.
    public static bool SameTeamServer(Component fixture, ulong clientId)
    {
        var cafe = Of(fixture);
        return cafe != null && cafe.teamId >= 0 && PlayerTeam.Of(clientId) == cafe.teamId;
    }
}
