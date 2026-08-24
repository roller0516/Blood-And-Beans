using System.Collections.Generic;

/// Server-side hold accumulator, keyed by client.
///
/// The client reports only "started pressing" and "let go"; the elapsed time is measured
/// here against the server clock. Anything that timed a hold on the owner was skippable
/// by a client that simply sent the completion RPC early — which was true of the whole
/// night loot loop (아키텍처_v1.0.md §1.1).
///
/// No UnityEngine dependency on purpose: this is a rule, and rules are tested without a
/// scene (아키텍처_v1.0.md §2.1).
public class HoldTimer
{
    readonly Dictionary<ulong, double> startedAt = new();

    public bool Holding(ulong client) => startedAt.ContainsKey(client);

    /// A repeat Begin is ignored. Otherwise a client could spam it to keep progress
    /// pinned at zero — or worse, replace a legitimate hold it never paid for.
    public void Begin(ulong client, double now)
    {
        if (!startedAt.ContainsKey(client)) startedAt[client] = now;
    }

    public void Cancel(ulong client) => startedAt.Remove(client);

    public void CancelAll() => startedAt.Clear();

    public float Elapsed(ulong client, double now) =>
        startedAt.TryGetValue(client, out var t) ? (float)(now - t) : 0f;

    /// True once `seconds` have passed, and restarts the clock on success — so one held
    /// key yields one item per `seconds` (doc 6.5.1: 담기 개당 0.2초, 연속).
    public bool TryConsume(ulong client, double now, float seconds)
    {
        if (!startedAt.TryGetValue(client, out var t)) return false;
        if (now - t < seconds) return false;
        startedAt[client] = now;
        return true;
    }

    /// A dash breaks off an open but leaves half the progress (doc 6.6).
    public void Halve(ulong client, double now)
    {
        if (!startedAt.TryGetValue(client, out var t)) return;
        startedAt[client] = now - (now - t) * 0.5d;
    }

    /// Copies the current holders into a caller-owned list. The caller must not mutate
    /// this timer while enumerating it directly, and per-frame allocation is not free —
    /// so the iteration set is snapshotted instead.
    public void CopyClientsTo(List<ulong> outp)
    {
        outp.Clear();
        foreach (var pair in startedAt) outp.Add(pair.Key);
    }
}
