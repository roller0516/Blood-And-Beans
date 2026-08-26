using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// 주변의 상호작용 후보를 들고 있는다. 입력은 BB.Client가 넣어 준다.
public class PlayerInteractor : NetworkBehaviour
{
    readonly List<IInteractable> candidates = new();
    IInteractable current;

    public string Prompt => Nearest()?.Prompt ?? string.Empty;

    /// 지금 상호작용 중인 대상. `BeginClient`와 `EndClient` 사이에만 있다. 어떤 박스를
    /// 잡고 있는지 아는 유일한 지점이라 루팅 창을 여닫는 쪽이 여기를 읽는다.
    public IInteractable Current => current;

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
