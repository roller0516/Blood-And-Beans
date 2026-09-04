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

    /// 마지막으로 F를 누른 대상. `Current`와 달리 F를 놓아도 남는다 — 재료 칸의 그리드
    /// 창은 누르고 있는 동안이 아니라 닫을 때까지 떠 있다 (기획서 6.5.4). 창을 여는 쪽이
    /// 거리를 다시 확인하므로, 여기 남아 있다는 것만으로 창이 뜨지는 않는다.
    public IInteractable Latest { get; private set; }

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
        if (current != null) Latest = current;
        current?.BeginInteractionClient();
    }

    public void EndClient()
    {
        // 잡고 있던 대상이 홀드 도중 디스폰될 수 있다(가방을 다 파내면 서버가 바로 없앤다).
        // `current`는 인터페이스 참조라 `?.`가 Unity의 파괴 판정을 타지 않으므로, 여기서
        // 직접 걸러야 스폰되지 않은 NetworkBehaviour에 RPC를 보내지 않는다.
        if (current is NetworkBehaviour target && (target == null || !target.IsSpawned)) current = null;

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
