using System.Collections.Generic;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// 아이템 치트. **재료와 수량을 위에서 한 번 고르고, 아래에서 어디에 넣을지 누른다.**
/// 가방(밤)과 팀 재고(낮) 둘 다 한 창에서 채울 수 있어야 한다 — 밤에 캐서 귀환하는
/// 정규 경로는 한 판이 걸리므로, 그걸 기다리면 낮 조리·보석 효과를 볼 수 없다.
///
/// 재료가 16종이라 순환 버튼이 아니라 목록이다 (`CharacterCheatGroup`과 같은 이유).
/// 목록 맨 위의 「전체」는 16종을 한 번에 넣는다 — 낮 조리는 메뉴마다 재료가 다르므로
/// (기획서 7.2) 하나씩 골라 넣으면 메뉴 하나 만들기까지 열 번을 누른다.
///
/// 보석을 재고에 넣으면 그 자리에서 <see cref="Cafe.ApplyHarvestGemsServer"/>까지 돈다.
/// 재고에 놓인 보석은 정산이 소모해야 효과가 켜지므로(기획서 8.1), 넣기만 하면 재고에
/// 숫자만 쌓이고 아무 일도 일어나지 않는다.
///
/// 가방과 재고는 서버 권위라 서버(호스트)에서만 열린다. 클라이언트에서 눌러 봐야 서버
/// 상태는 그대로이고, 그걸 모른 채 테스트하면 없는 결함을 쫓게 된다.
public class ItemCheatGroup : DevConsoleGroup
{
    public override string Tab => "치트";
    public override string Title => "아이템";

    Label note;
    Button pick;
    IntegerField amount;

    /// 사람·팀마다 한 줄. 명단이 바뀔 때만 다시 만든다 — `Refresh`는 10Hz라 매번 다시
    /// 만들면 버튼을 누르는 순간 그 버튼이 사라지고 목록도 함께 닫힌다.
    VisualElement bags, stocks;

    readonly List<ulong> shownClients = new();
    readonly List<Label> bagValues = new();
    readonly List<Button> bagAdds = new(), bagClears = new();

    int shownTeams = -1;
    readonly List<Label> stockValues = new();
    readonly List<Button> stockAdds = new();

    /// 고른 재료. <see cref="Ingredient.None"/>은 「전체」다 — 열거자가 이미 "없음"을
    /// 그 값으로 쓰고 있어서(`Ingredients`) 치트 쪽에 따로 센티넬을 만들 이유가 없다.
    Ingredient selected = Ingredient.Milk;
    MatchDirector director;

    protected override void Build(VisualElement group)
    {
        note = Row(group, "상태", "-");

        var pickRow = ButtonRow(group);
        pick = Btn(pickRow, "-", OpenItemList);

        amount = FieldRow(group, "수량", 5);

        bags = Section(group, "가방");
        stocks = Section(group, "팀 재고");
    }

    static VisualElement Section(VisualElement parent, string title)
    {
        var label = new Label(title);
        label.AddToClassList("hint");
        parent.Add(label);

        var box = new VisualElement();
        parent.Add(box);
        return box;
    }

    public override void Refresh(in DevConsoleState state)
    {
        pick.text = $"{NameOf(selected)}  ▾";

        // 재생을 벗어나면 파괴된 오브젝트를 붙들고 있지 않는다. 조립 루트 조회는 캐시가
        // 비면 FindAnyObjectByType으로 떨어지므로 서버일 때만 찾는다.
        if (!state.Playing) director = null;

        if (!state.IsServer)
        {
            note.text = state.Listening ? "서버에서만 (호스트로 실행)" : "접속 전";
            Clear();
            return;
        }

        if (director == null) director = MatchDirector.Instance;

        var manager = NetworkManager.Singleton;
        if (manager == null) { note.text = "-"; Clear(); return; }

        var teams = state.Seating != null ? state.Seating.TeamCount : 0;
        if (!SameClients(manager)) RebuildBags(manager);
        if (teams != shownTeams) RebuildStocks(teams);

        note.text = $"{shownClients.Count}명 · {shownTeams}팀";

        RefreshBags();
        RefreshStocks();
    }

    // ── 가방 ──────────────────────────────────────────────────────

    /// 접속 명단이 그대로인가. 순서까지 같아야 버튼이 엉뚱한 사람을 가리키지 않는다.
    bool SameClients(NetworkManager manager)
    {
        var clients = manager.ConnectedClientsList;
        if (clients.Count != shownClients.Count) return false;

        for (var i = 0; i < clients.Count; i++)
            if (clients[i].ClientId != shownClients[i]) return false;

        return true;
    }

    void RebuildBags(NetworkManager manager)
    {
        shownClients.Clear();
        bagValues.Clear();
        bagAdds.Clear();
        bagClears.Clear();
        bags.Clear();

        foreach (var client in manager.ConnectedClientsList)
        {
            var id = client.ClientId;
            shownClients.Add(id);

            bagValues.Add(Row(bags, $"#{id} · {DisplayNames.Team(PlayerTeam.Of(id))}", "-"));

            var buttons = ButtonRow(bags);
            bagAdds.Add(Btn(buttons, "넣기", () => AddToBag(id), "btn--primary"));
            bagClears.Add(Btn(buttons, "비우기", () => ClearBag(id), "btn--danger"));
        }
    }

    void RefreshBags()
    {
        for (var i = 0; i < shownClients.Count; i++)
        {
            var inv = InventoryOf(shownClients[i]);
            var usable = inv != null && inv.HasBag;

            bagValues[i].text = inv == null ? "스폰 대기"
                : !usable ? "가방 묻어 둠"
                : $"{inv.Carried:0.#}/{inv.Capacity:0.#}kg · {inv.Count}개"
                  + (inv.Overloaded ? " · 과적" : string.Empty);

            bagAdds[i].SetEnabled(usable);
            bagClears[i].SetEnabled(usable);
        }
    }

    void AddToBag(ulong clientId)
    {
        var inv = InventoryOf(clientId);
        if (inv == null) return;

        var count = Mathf.Max(1, amount.value);
        foreach (var item in Chosen()) inv.AddServer(item, count);
    }

    void ClearBag(ulong clientId) => InventoryOf(clientId)?.ClearServer();

    static PlayerInventory InventoryOf(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.ConnectedClients.TryGetValue(clientId, out var c)) return null;
        return c.PlayerObject != null ? c.PlayerObject.GetComponent<PlayerInventory>() : null;
    }

    // ── 팀 재고 ───────────────────────────────────────────────────

    void RebuildStocks(int teams)
    {
        shownTeams = teams;
        stockValues.Clear();
        stockAdds.Clear();
        stocks.Clear();

        for (var t = 0; t < teams; t++)
        {
            var team = t;
            stockValues.Add(Row(stocks, DisplayNames.Team(team), "-"));

            var buttons = ButtonRow(stocks);
            stockAdds.Add(Btn(buttons, "넣기", () => AddToStock(team), "btn--primary"));
        }
    }

    void RefreshStocks()
    {
        for (var t = 0; t < stockValues.Count; t++)
        {
            var stock = StockOf(t);
            if (stock == null)
            {
                stockValues[t].text = "카페 대기";
            }
            else
            {
                // 「전체」면 고른 것 전부의 합이다. 한 종류면 그 종류의 수량 그대로다.
                var total = 0;
                foreach (var item in Chosen()) total += stock.CountOf(item);
                stockValues[t].text = $"{NameOf(selected)} {total}개";
            }
            stockAdds[t].SetEnabled(stock != null);
        }
    }

    void AddToStock(int team)
    {
        var cafe = director != null ? director.CafeOf(team) : null;
        var stock = cafe != null ? cafe.Stock : null;
        if (stock == null) return;

        var gem = false;
        foreach (var item in Chosen())
        {
            for (var i = Mathf.Max(1, amount.value); i > 0; i--) stock.DepositServer(item);
            gem |= Gems.IsGem(item);
        }

        // 보석은 재고에 쌓여만 있으면 아무 효과도 없다. 정산이 하는 소모를 여기서 대신한다.
        if (gem) cafe.ApplyHarvestGemsServer();
    }

    TeamStock StockOf(int team)
    {
        var cafe = director != null ? director.CafeOf(team) : null;
        return cafe != null ? cafe.Stock : null;
    }

    // ── 재료 목록 ─────────────────────────────────────────────────

    /// 숲 재료 · 상비 재료 · 보석을 갈라 놓는다. 한 줄로 늘어놓으면 3등급 상자에만
    /// 나오는 것과 항상 있는 것이 섞여 무엇을 고르는지 알기 어렵다.
    void OpenItemList()
    {
        var menu = new GenericMenu();

        menu.AddItem(new GUIContent("전체"), selected == Ingredient.None,
                     () => selected = Ingredient.None);
        menu.AddSeparator(string.Empty);

        foreach (Ingredient item in System.Enum.GetValues(typeof(Ingredient)))
        {
            if (item == Ingredient.None) continue;

            var value = item;
            var folder = Gems.IsGem(item) ? "보석" : Ingredients.IsStaple(item) ? "상비" : "숲";
            var label = $"{folder}/{DisplayNames.Of(item)} · {Ingredients.WeightOf(item):0.#}kg";

            menu.AddItem(new GUIContent(label), selected == value, () => selected = value);
        }

        // UIElements의 `worldBound`는 패널 좌표이고 `GenericMenu`는 GUI 좌표를 받는다.
        // 에디터 창 안에서는 둘이 같은 원점을 쓰므로 그대로 넘긴다.
        menu.DropDown(pick.worldBound);
    }

    /// 지금 넣게 될 재료들. 한 종류를 골랐으면 그것 하나, 「전체」면 16종 전부다.
    /// 넣는 쪽과 세는 쪽이 같은 목록을 보게 하려고 한 곳에서 돌려준다.
    IEnumerable<Ingredient> Chosen()
    {
        if (selected != Ingredient.None) { yield return selected; yield break; }

        foreach (Ingredient item in System.Enum.GetValues(typeof(Ingredient)))
            if (item != Ingredient.None) yield return item;
    }

    static string NameOf(Ingredient item) =>
        item == Ingredient.None ? "전체" : DisplayNames.Of(item);

    void Clear()
    {
        shownClients.Clear();
        bagValues.Clear();
        bagAdds.Clear();
        bagClears.Clear();
        bags?.Clear();

        shownTeams = -1;
        stockValues.Clear();
        stockAdds.Clear();
        stocks?.Clear();
    }
}
