using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// 카페 전용 보관대. 완성품은 개수 제한 없이 내려놓고 먼저 둔 것부터 받는다 (5.4-13).
public class PrepIsland : NetworkBehaviour, IInteractable, IItemHolder
{
    [SerializeField] float reach = 2.5f;
    readonly List<HeldItem> items = new();
    readonly NetworkList<CarryView> views = new();
    Cafe cafe;
    Collider surface;
    public event System.Action ContentsChanged;
    public int SlotCount => views.Count;
    public int HighlightSlot => -1;
    public CarryView SlotAt(int index) => index >= 0 && index < views.Count ? views[index] : CarryView.Nothing;
    public string Prompt => views.Count == 0 ? "보관대 · F로 내려놓기" : $"보관대 · {views.Count}개 · F로 놓기/가져가기";
    void Awake() { cafe = Cafe.Of(this); surface = GetComponentInChildren<Collider>(true); }
    public override void OnNetworkSpawn()
    {
        views.OnListChanged += Changed;
        if (cafe != null && cafe.Director != null) cafe.Director.Phase.PhaseEntered += OnPhase;
    }
    public override void OnNetworkDespawn()
    {
        views.OnListChanged -= Changed;
        if (cafe != null && cafe.Director != null) cafe.Director.Phase.PhaseEntered -= OnPhase;
    }
    void Changed(NetworkListEvent<CarryView> _) => ContentsChanged?.Invoke();
    void OnPhase(Phase phase)
    {
        if (!IsServer || phase == Phase.Day) return;
        foreach (var item in items) if (item.IsProduct || item.HasDish) cafe.Dishes.SoilServer(item.DishIsPlate);
        items.Clear(); views.Clear();
    }
    public void BeginInteractionClient() => UseRpc();
    public void EndInteractionClient() { }
    [Rpc(SendTo.Server)]
    public void UseRpc(RpcParams p = default)
    {
        var id = p.Receive.SenderClientId;
        if (cafe == null || cafe.Director == null || cafe.Director.Phase.Current != Phase.Day || !Cafe.SameTeamServer(this, id)) return;
        var carry = PlayerCarry.Of(id);
        if (carry == null || carry.Reserved || !Station.WithinReach(surface, transform, carry.transform.position, reach)) return;
        if (!carry.Empty)
        {
            items.Add(carry.Held); views.Add(CarryView.Of(carry.Held)); carry.ClearServer();
        }
        else if (items.Count > 0)
        {
            carry.SetServer(items[0]); items.RemoveAt(0); views.RemoveAt(0);
        }
    }
}
