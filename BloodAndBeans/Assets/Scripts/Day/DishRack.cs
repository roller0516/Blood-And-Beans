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
    /// 깨끗한 것 **하나마다 하나씩** 선다. 개수를 UI로 세지 않고 선반을 보면 끝나야 한다
    /// (기획서 5.7.3). 칸을 1로 두면 둘이 남았는지 하나가 남았는지가 화면에서 같아 보인다.
    int Clean => cafe != null && cafe.Dishes != null ? (plate ? cafe.Dishes.CleanPlates : cafe.Dishes.CleanCups) : 0;
    public int SlotCount => Clean;
    public int HighlightSlot => -1;
    public CarryView SlotAt(int slot) => slot >= 0 && slot < Clean
        ? CarryView.Of(HeldItem.Dish(plate)) : CarryView.Nothing;
    public override void OnNetworkSpawn() { if (cafe?.Dishes != null) cafe.Dishes.CleanChanged += OnClean; OnClean(); }
    public override void OnNetworkDespawn() { if (cafe?.Dishes != null) cafe.Dishes.CleanChanged -= OnClean; }
    void OnClean() => ContentsChanged?.Invoke();
    public string Prompt => plate ? "접시 · F로 가져가기" : "잔 · F로 가져가기";
    void Awake() { cafe = Cafe.Of(this); surface = GetComponentInChildren<Collider>(true); }
    public void BeginInteractionClient() => TakeRpc();
    public void EndInteractionClient() { }
    public bool CanPromptClient(in InteractionContext ctx) => !ctx.Reserved && ctx.Held.Empty && !SlotAt(0).Empty;
    public string PromptFor(in InteractionContext ctx) => Prompt;
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
            PlayerController.ReportSuccessServer(id, this);
        }
    }
}
