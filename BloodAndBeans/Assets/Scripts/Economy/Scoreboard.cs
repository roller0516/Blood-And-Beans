using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// Cumulative sales per team + live rank ticker (design doc 3.1).
/// Only revenue is replicated — ingredients, equipment and characters stay
/// private, so nothing else belongs in this component.
public class Scoreboard : NetworkBehaviour
{
    // Team count comes from MatchDirector. Counting tills here was one of the six
    // independent answers to "how many teams" (아키텍처_v1.0.md §1.4).
    readonly NetworkList<int> revenue = new();

    public int TeamCount => revenue.Count;
    public int RevenueOf(int team) => revenue[team];

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        revenue.Clear();                       // respawn must not stack extra teams
        var director = MatchDirector.Find();
        var teams = director != null ? director.TeamCount : 1;
        for (var i = 0; i < teams; i++) revenue.Add(0);
    }

    /// Server-only. amount comes straight from SalePrice.Calculate.
    public void AddSale(int team, int amount)
    {
        if (!IsServer) return;
        revenue[team] += amount;
    }

    /// Team indices, richest first. ponytail: allocates a list per call —
    /// fine at <=4 teams and one ticker refresh per frame at worst.
    public List<int> Ranking()
    {
        var order = new List<int>(revenue.Count);
        for (var i = 0; i < revenue.Count; i++) order.Add(i);
        order.Sort((a, b) => revenue[b].CompareTo(revenue[a]));
        return order;
    }
}
