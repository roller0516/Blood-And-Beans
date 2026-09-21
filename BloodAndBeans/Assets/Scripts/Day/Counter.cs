using Unity.Netcode;
using UnityEngine;

/// 자기 카페의 손님에게 완성품을 서빙한다 (기획서 5.4-10).
public class Counter : NetworkBehaviour, IInteractable, IItemHolder
{
    [SerializeField] float reach = 2.5f;
    Cafe cafe;
    Collider surface;
    public string Prompt => "서빙대 · F로 서빙";
    public event System.Action ContentsChanged { add { } remove { } }
    public int SlotCount => 0;
    public int HighlightSlot => -1;
    public CarryView SlotAt(int index) => CarryView.Nothing;
    void Awake() { cafe = Cafe.Of(this); surface = GetComponentInChildren<Collider>(true); }
    public void BeginInteractionClient() => ServeRpc();
    public void EndInteractionClient() { }
    [Rpc(SendTo.Server)]
    public void ServeRpc(RpcParams p = default)
    {
        var id = p.Receive.SenderClientId;
        if (cafe == null || cafe.Director.Phase.Current != Phase.Day || !Cafe.SameTeamServer(this, id)) return;
        var carry = PlayerCarry.Of(id);
        if (carry == null || carry.Reserved || !carry.Held.IsProduct) return;
        if (!Station.WithinReach(surface, transform, carry.transform.position, reach)) return;
        if (cafe.Queue != null && cafe.Queue.TryServeServer(carry.Held))
        {
            carry.SetServer(HeldItem.Dish(carry.Held.DishIsPlate, true));
            PlayerInteractor.ReportSuccessServer(id, this);
        }
    }
}
