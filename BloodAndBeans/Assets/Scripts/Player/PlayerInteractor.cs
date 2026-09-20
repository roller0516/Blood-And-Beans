using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// 주변의 상호작용 후보를 들고 있는다. 입력은 BB.Client가 넣어 준다.
public class PlayerInteractor : NetworkBehaviour
{
    readonly List<IInteractable> candidates = new();
    IInteractable current;
    PlayerCarry localCarry;
    void Awake() => localCarry = GetComponent<PlayerCarry>();

    public string Prompt
    {
        get
        {
            var target = Nearest();
            if (target is SharedFacility facility && localCarry != null && localCarry.View.Dirty && !facility.Busy && facility.Kind != FacilityKind.Sink)
                return "세척 필요";
            return target?.Prompt ?? string.Empty;
        }
    }

    /// 지금 상호작용 중인 대상. `BeginClient`와 `EndClient` 사이에만 있다. 어떤 박스를
    /// 잡고 있는지 아는 유일한 지점이라 루팅 창을 여닫는 쪽이 여기를 읽는다.
    public IInteractable Current => current;

    /// 지금 F가 닿는 대상. **프롬프트가 가리키는 것과 같은 것**이고, 테두리(`TargetOutline`)도
    /// 여기를 읽는다 — 안내와 테두리가 서로 다른 설비를 가리키면 둘 다 못 믿게 된다.
    /// `Prompt`와 같은 판정을 한 번 더 도는 것이라 손에 든 것으로 막힌 설비는 여기도 안 잡힌다.
    public IInteractable Target => Nearest();

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

    bool CanPrompt(MonoBehaviour target)
    {
        var director = MatchDirector.Instance;
        if (director == null || director.Phase.Current != Phase.Day || localCarry == null) return true;
        var held = localCarry.View;
        if (target is SharedFacility facility)
            return facility.Busy || (!localCarry.Reserved && CanUseFacility(facility.Kind, held, director.CafeOf(PlayerTeam.Local())?.Dishes?.Dirty ?? 0));
        if (localCarry.Reserved) return false;
        if (target is DishRack rack) return held.Empty && !rack.SlotAt(0).Empty;
        if (target is Counter) return held.IsProduct;
        if (target is PrepIsland island) return !held.Empty || island.SlotCount > 0;
        if (target is IngredientShelf shelf)
            return shelf.LocalPlayerNear && shelf.SlotCountAt(0) > 0 && !held.Dirty &&
                (held.IsProduct || (shelf.SlotItem(0) == Ingredient.BloodBean && held.HasDish && !held.DishIsPlate &&
                    (held.Ingredient == Ingredient.None || held.Ingredient == Ingredient.Bean)));
        if (target is PlayerCarry other) return !other.Reserved && (!held.Empty || !other.View.Empty);
        return true;
    }
    // 기획서 5.7.4: 손 상태로 불가능한 프롬프트는 숨긴다. 실행 권한은 RPC가 재검증한다.
    public static bool CanUseFacility(FacilityKind kind, CarryView held, int dirtyStock = 0)
    {
        if (kind == FacilityKind.Sink && held.Empty) return dirtyStock > 0;
        if (held.HasDish && held.Dirty) return true; // 「세척 필요」는 예외 안내다.
        return SharedFacility.Accepts(kind, held);
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
            if (behaviour is PlayerCarry carry &&
                (carry.OwnerClientId == OwnerClientId || PlayerTeam.Of(carry.OwnerClientId) != PlayerTeam.Local())) continue;
            if (!CanPrompt(behaviour)) continue;
            var distance = Vector3.SqrMagnitude(transform.position - behaviour.transform.position);
            if (distance >= bestDistance) continue;
            best = candidates[i];
            bestDistance = distance;
        }
        return best;
    }
}
