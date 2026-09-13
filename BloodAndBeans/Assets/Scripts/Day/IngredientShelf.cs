using Unity.Netcode;
using UnityEngine;

/// 자기 카페에서 수집 재료를 완성품에 마무리한다 (기획서 5.1).
public class IngredientShelf : NetworkBehaviour, IInteractable, IItemHolder, ILootGrid
{
    [SerializeField] Ingredient[] offer = { Ingredient.Milk };
    [SerializeField] float reach = 2.5f;
    Cafe ownerCafe;
    Collider surface;
    TeamStock Stock => ownerCafe != null ? ownerCafe.Stock : null;
    MatchDirector Director => ownerCafe != null ? ownerCafe.Director : null;
    public bool Enabled => isActiveAndEnabled;
    public bool GridOpen { get; private set; }
    public float Reach => reach;
    public bool LocalPlayerNear => Station.LocalPlayerNear(surface, transform, reach);
    public string Prompt => offer.Length == 1 ? $"{DisplayNames.Of(offer[0])} · F 홀드로 마무리" : "수집 재료 · F로 열기/닫기";
    public string GridTitle => "수집 재료";
    public string GridHint => "완성품에 추가할 재료 선택 · F로 닫기";
    public int SlotCount => offer.Length;
    public int RevealedCount => SlotCount;
    public int HighlightSlot => -1;
    public event System.Action ContentsChanged;
    public Ingredient SlotItem(int slot) => slot >= 0 && slot < offer.Length ? offer[slot] : Ingredient.None;
    public int SlotCountAt(int slot) => Stock != null ? Stock.CountOf(SlotItem(slot)) : 0;
    public CarryView SlotAt(int slot) => SlotCountAt(slot) > 0 ? CarryView.Of(SlotItem(slot)) : CarryView.Nothing;
    public void CloseGridClient() => GridOpen = false;
    public void TakeSlotClient(int slot) { if (slot >= 0 && slot < offer.Length) TakeRpc((int)offer[slot]); }
    public void BeginInteractionClient()
    {
        if (offer.Length == 1) TakeRpc((int)offer[0]); else GridOpen = !GridOpen;
    }
    public void EndInteractionClient() => CancelFinishRpc();
    void Awake() { ownerCafe = Cafe.Of(this); surface = GetComponentInChildren<Collider>(true); }
    public override void OnNetworkSpawn() { if (Stock != null) Stock.CountsChanged += OnStock; }
    public override void OnNetworkDespawn()
    {
        if (Stock != null) Stock.CountsChanged -= OnStock;
        if (!IsServer) return;
        finishClients.Clear(); finishClients.AddRange(finishing.Keys);
        foreach (var id in finishClients) CancelFinishServer(id);
    }
    void OnStock() => ContentsChanged?.Invoke();
    readonly System.Collections.Generic.Dictionary<ulong, (PlayerCarry carry, Ingredient item, double at)> finishing = new();
    [Rpc(SendTo.Server)]
    void CancelFinishRpc(RpcParams p = default) => CancelFinishServer(p.Receive.SenderClientId);
    void CancelFinishServer(ulong id)
    {
        if (finishing.TryGetValue(id, out var pending) && pending.carry != null) pending.carry.ReserveServer(false);
        finishing.Remove(id);
    }
    readonly System.Collections.Generic.List<ulong> finishClients = new();
    void Update()
    {
        if (!IsServer || finishing.Count == 0) return;
        finishClients.Clear();
        finishClients.AddRange(finishing.Keys);
        foreach (var id in finishClients)
        {
            var pending = finishing[id];
            if (pending.carry == null || !pending.carry.IsSpawned || Director == null || Director.Phase.Current != Phase.Day ||
                !Station.WithinReach(surface, transform, pending.carry.transform.position, reach))
            { CancelFinishServer(id); continue; }
            if (NetworkManager.ServerTime.Time < pending.at) continue;
            if (Stock != null && Stock.TakeServer(pending.item))
            {
                var product = pending.carry.Held;
                var parts = new Ingredient[product.Recipe.Length + 1];
                System.Array.Copy(product.Recipe, parts, product.Recipe.Length);
                parts[parts.Length - 1] = pending.item;
                product.Recipe = parts;
                product.Menu = Menus.Match(parts);
                pending.carry.SetServer(product);
            }
            CancelFinishServer(id);
        }
    }

    [Rpc(SendTo.Server)]
    public void TakeRpc(int ingredient, RpcParams p = default)
    {
        var clientId = p.Receive.SenderClientId;
        if (!Enabled) return;                                // 아직 설치되지 않은 칸이다
        if (Director == null || Director.Phase.Current != Phase.Day) return;
        if (!NetworkManager.ConnectedClients.TryGetValue(clientId, out var c)) return;

        var po = c.PlayerObject;
        if (po == null) return;
        if (!Station.WithinReach(surface, transform, po.transform.position, reach)) return;
        if (!Cafe.SameTeamServer(this, clientId)) return;     // 내 재고가 아니다
        var carry = PlayerCarry.Of(clientId);
        if (carry == null || carry.Reserved) return;

        var want = (Ingredient)ingredient;
        if (System.Array.IndexOf(offer, want) < 0) return;   // 이 선반에 없는 재료다

        if (TeamBuffs.IndexOf(want) >= 0 || Ingredients.IsStaple(want)) return;
        if (carry.Held.IsProduct)
        {
            var recipe = carry.Held.Recipe;
            if (recipe == null || System.Array.IndexOf(recipe, want) >= 0 || want == Ingredient.BloodBean) return;
            var dessert = System.Array.IndexOf(recipe, Ingredient.BreadBase) >= 0;
            if (dessert ? !Menus.IsDessertTopping(want) || recipe.Length >= Menus.MaxDessertParts :
                want != Ingredient.Milk && want != Ingredient.Cream && want != Ingredient.Chocolate && want != Ingredient.Ice) return;
            if (recipe.Length >= 4 || Stock == null || Stock.CountOf(want) <= 0) return;
            // ponytail: 마무리 시간은 14장 #42 미결. 임시 1초, 확정 시 DayBalance로 이관한다.
            var seconds = (ownerCafe != null && ownerCafe.HasBuff(TeamBuff.Finish) ? 1f / DayBalance.BuffSpeed : 1f)
                * (PlayerCharacter.Of(clientId)?.WorkScale(4) ?? 1f);
            finishing[clientId] = (carry, want, NetworkManager.ServerTime.Time + seconds);
            carry.ReserveServer(true);
            return;
        }
        if (want == Ingredient.BloodBean && carry.Held.HasDish && !carry.Held.Dirty && !carry.Held.DishIsPlate &&
            (carry.Held.Ingredient == Ingredient.None || carry.Held.Ingredient == Ingredient.Bean))
        {
            if (Stock == null || !Stock.TakeServer(want)) return;
            var replacement = HeldItem.Of(want);
            replacement.HasDish = true;
            carry.SetServer(replacement);
            return;
        }

    }
}
