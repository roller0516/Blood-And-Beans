using Unity.Netcode;
using UnityEngine;

/// 카페에서 팀 소유 식기를 준비해 광장으로 운반한다 (기획서 5.1·5.3).
public sealed class DishRack : NetworkBehaviour, IInteractable, IItemHolder
{
    [SerializeField] bool plate;
    [SerializeField] float reach = 2.5f;
    Cafe cafe;
    Collider surface;
    public event System.Action ContentsChanged;
    public int SlotCount => 1;
    public int HighlightSlot => -1;
    public CarryView SlotAt(int slot) => slot == 0 && cafe != null && cafe.Dishes != null &&
        (plate ? cafe.Dishes.CleanPlates : cafe.Dishes.CleanCups) > 0
        ? CarryView.Of(HeldItem.Dish(plate)) : CarryView.Nothing;
    public override void OnNetworkSpawn() { if (cafe?.Dishes != null) cafe.Dishes.CleanChanged += OnClean; OnClean(); }
    public override void OnNetworkDespawn() { if (cafe?.Dishes != null) cafe.Dishes.CleanChanged -= OnClean; }
    void OnClean() => ContentsChanged?.Invoke();
    public string Prompt => plate ? "접시 · F로 가져가기" : "잔 · F로 가져가기";
    void Awake() { cafe = Cafe.Of(this); surface = GetComponentInChildren<Collider>(true); }
    public void BeginInteractionClient() => TakeRpc();
    public void EndInteractionClient() { }
    [Rpc(SendTo.Server)]
    public void TakeRpc(RpcParams p = default)
    {
        var id = p.Receive.SenderClientId;
        if (cafe == null || cafe.Director == null || cafe.Director.Phase.Current != Phase.Day ||
            !Cafe.SameTeamServer(this, id)) return;
        var carry = PlayerCarry.Of(id);
        if (carry == null || carry.Reserved || !carry.Empty ||
            !Station.WithinReach(surface, transform, carry.transform.position, reach)) return;
        if (cafe.Dishes != null && cafe.Dishes.ClaimServer(plate))
        {
            carry.SetServer(HeldItem.Dish(plate));
            PlayerInteractor.ReportSuccessServer(id, this);
        }
    }
}
