using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// Keeps nearby interaction candidates; input is supplied by BB.Client.
public class PlayerInteractor : NetworkBehaviour
{
    readonly List<IInteractable> candidates = new();
    IInteractable current;

    public string Prompt => Nearest()?.Prompt ?? string.Empty;

    void OnTriggerEnter(Collider other)
    {
        if (!IsOwner) return;
        foreach (var behaviour in other.GetComponentsInParent<MonoBehaviour>())
            if (behaviour is IInteractable candidate && !candidates.Contains(candidate))
                candidates.Add(candidate);
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsOwner) return;
        foreach (var behaviour in other.GetComponentsInParent<MonoBehaviour>())
            if (behaviour is IInteractable candidate) candidates.Remove(candidate);
    }

    public void BeginClient()
    {
        if (!IsOwner) return;
        if (CompletionGauge.TryStopLocalClient()) return;

        current = Nearest();
        current?.BeginInteractionClient();
    }

    public void EndClient()
    {
        current?.EndInteractionClient();
        current = null;
    }

    public void DumpClient()
    {
        if (!IsOwner) return;
        var sink = Nearest() as Sink;
        if (sink != null) sink.DiscardRpc();
        else GetComponent<PlayerInventory>()?.DumpRpc();
    }

    IInteractable Nearest()
    {
        IInteractable best = null;
        var bestDistance = float.MaxValue;
        for (var i = candidates.Count - 1; i >= 0; i--)
        {
            if (candidates[i] is not MonoBehaviour behaviour || behaviour == null)
            {
                candidates.RemoveAt(i);
                continue;
            }
            var distance = Vector3.SqrMagnitude(transform.position - behaviour.transform.position);
            if (distance >= bestDistance) continue;
            best = candidates[i];
            bestDistance = distance;
        }
        return best;
    }
}
