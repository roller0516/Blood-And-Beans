using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// Economy가 판매 하나의 가격을 매기는 데 필요한 전부 (기획서 5.6.2). 낮은 게이지와
/// 레시피와 손님을 알지만 임대료도 예보도 원두 등급표도 모른다. 그래서 이 값만 넘기고
/// 계산에는 관여하지 않는다.
public struct ServeInfo
{
    public MenuId Menu;
    public Ingredient[] Recipe;
    public float GaugeMultiplier;   // 1.3 / 1.0 / 0.7 / 0.3 (탄 것)
    public bool Burnt;
    public Race Kind;
    public float RacePriceWeight;
    public int BasePrice;
}

/// 대기 줄 (기획서 5.5). 손님을 스폰하고, 인내심이 끝나면 내보내고, 서빙된 물건을
/// 오직 태그로만 주문과 대조한다.
public class CustomerQueue : NetworkBehaviour
{
    [SerializeField] Customer customerPrefab;
    [SerializeField] int maxWaiting = 4;
    [SerializeField] float spawnSeconds = 8f;   // ponytail: 임시값, 기획서 14장 #1
    [SerializeField] float slotSpacing = 1.5f;

    // ponytail: 탄 것을 팔았을 때의 인내심 감소는 기획서 14장 #6, 아직 미결정이다.
    [SerializeField] float burntPatiencePenalty = 10f;

    readonly List<Customer> waiting = new();
    public IReadOnlyList<Customer> Waiting => waiting;
    double nextSpawn;

    Cafe ownerCafe;
    GamePhase clock;

    /// 시계를 지연 해석한다. 예전에는 `[SerializeField] GamePhase`였는데, 이 컴포넌트는
    /// 런타임에 스폰되는 카페 프리팹 안에 있어서 씬 오브젝트를 직렬화 참조로 가질 수 없다.
    /// 프리팹에는 `{fileID: 0}`이 박혔고, 그래서 "손님은 낮에만" 가드가 통째로 죽어 있었다 —
    /// 전환 10초 동안 손님이 미리 나와 인내심을 까먹었다.
    /// 조립 루트는 설비들과 같은 통로(소속 카페)에서 받는다 (아키텍처_v1.0.md §1.4).
    GamePhase Clock => clock != null ? clock
        : (clock = (ownerCafe != null ? ownerCafe : (ownerCafe = Cafe.Of(this)))?.Director?.Phase);

    /// Economy가 이 이벤트를 구독한다. 낮은 최종 가격을 직접 계산하지 않는다.
    /// static이 아니라 대기열마다 하나씩 둔다. 카페가 둘일 때 static 이벤트는 모든 팀의
    /// 판매를 먼저 구독한 계산대 하나에 몰아넣었다.
    public event System.Action<ServeInfo> Served;

    void Update()
    {
        if (!IsServer) return;

        // 손님은 낮에만 존재한다. 시계를 아직 못 찾았으면 낮이라고 단정하지 않는다 —
        // 모를 때 손님을 내보내는 쪽이 이 가드가 없던 예전 동작이다.
        var now = Clock;
        if (now == null || now.Current != Phase.Day)
        {
            ClearAll();
            servedFirstToday = false;   // 다음 낮의 첫 손님에게 「붙임성」이 다시 걸린다
            return;
        }

        for (int i = waiting.Count - 1; i >= 0; i--)
        {
            if (waiting[i] == null) { waiting.RemoveAt(i); continue; }
            if (waiting[i].Patience > 0f) continue;
            Leave(i);                      // 인내심 소진: 나가고 매출 0
        }

        if (waiting.Count >= maxWaiting || planned.Count == 0) return;
        if (NetworkManager.ServerTime.Time < nextSpawn) return;
        nextSpawn = NetworkManager.ServerTime.Time + spawnSeconds;
        Spawn();
    }

    void Spawn()
    {
        if (customerPrefab == null) return;

        var c = Instantiate(customerPrefab, SlotPosition(waiting.Count), transform.rotation);
        c.NetworkObject.SpawnWithObservers = false;
        c.NetworkObject.Spawn();
        // 소속 카페를 한 번 풀어서 팀 번호와 조립 루트를 같은 출처에서 받는다.
        var myCafe = Cafe.Of(this);
        var team = myCafe != null ? myCafe.TeamId : -1;
        myCafe?.Director?.ShowToTeamServer(c.NetworkObject, team);
        waiting.Add(c);

        var next = planned.Dequeue();
        var menu = Menus.All[next.menu];
        var count = next.race == Race.Werewolf ? Random.Range(2, 4) : 1;

        // 캐릭터 팀 패시브 (기획서 9.1). 손님 하나가 스폰될 때 한 번만 묻는다 — 손님이
        // 스스로 팀을 뒤지면 순회가 손님 수만큼 늘어난다.
        var patienceScale = PlayerCharacter.TeamHas(team, DayPassive.PopularCafe)
            ? DayPassives.PatienceBonus : 1f;

        // 「붙임성」은 *매장의* 첫 손님이다. 그날 처음 온 한 명에게만 걸린다.
        var welcoming = servedFirstToday == false
                     && PlayerCharacter.TeamHas(team, DayPassive.Welcoming);
        if (!servedFirstToday) servedFirstToday = true;

        c.SetupServer(team, next.race, Menus.TagsOf(menu.Parts), MenuTag.None,
                      menu.Parts.Length, count, patienceScale, welcoming);
    }

    /// 오늘 첫 손님을 이미 내보냈는가. 「붙임성」이 그 한 명에게만 걸린다 (기획서 9.1).
    bool servedFirstToday;

    readonly System.Collections.Generic.Queue<(Race race, int menu)> planned = new();

    /// 전환 화면은 내일의 손님 구성을 약속한다 (기획서 5.6). 그러니 대기열은 실제로 그
    /// 구성대로 손님을 내보내야 한다. 구성은 Economy가 만들고 여기서는 순서대로 소비한다.
    public void SetDayPlanServer(Forecast forecast)
    {
        if (!IsServer) return;
        planned.Clear();
        if (forecast?.Races == null || forecast.Orders == null) return;
        var count = Mathf.Min(forecast.Races.Length, forecast.Orders.Length);
        for (var i = 0; i < count; i++)
            if (forecast.Orders[i] >= 0 && forecast.Orders[i] < Menus.All.Length)
                planned.Enqueue((forecast.Races[i], forecast.Orders[i]));
    }

    Vector3 SlotPosition(int index) => transform.position + transform.right * (index * slotSpacing);

    void Leave(int index)
    {
        var c = waiting[index];
        waiting.RemoveAt(index);
        if (c != null && c.NetworkObject.IsSpawned) c.NetworkObject.Despawn();
        Reflow();
    }

    void ClearAll()
    {
        for (int i = waiting.Count - 1; i >= 0; i--) Leave(i);
    }

    void Reflow()
    {
        for (int i = 0; i < waiting.Count; i++)
            if (waiting[i] != null) waiting[i].transform.position = SlotPosition(i);
    }

    public void RestoreFrontServer()
    {
        if (!IsServer || waiting.Count == 0) return;
        // 최대치의 몫으로 회복시킨다. 종족표를 다시 읽으면 「인기 카페」로 늘어난 몫이
        // 빠져서, 패시브가 걸린 팀만 Perfect 회복이 상대적으로 작아진다.
        waiting[0].AddPatienceServer(waiting[0].PatienceMax * 0.25f);
    }

    /// 서버 권위 서빙. 누군가 물건을 받았으면 true를 돌려준다.
    /// 대조는 태그로 하며 메뉴 이름으로는 절대 하지 않는다 (기획서 7.2).
    public bool TryServeServer(HeldItem item)
    {
        if (!IsServer || !item.IsProduct || item.Recipe == null) return false;

        var tags = Menus.TagsOf(item.Recipe);
        var index = waiting.FindIndex(c => c != null && c.Accepts(tags, item.Recipe.Length));
        if (index < 0) return false;

        var c = waiting[index];
        Served?.Invoke(new ServeInfo
        {
            Menu = item.Menu,
            Recipe = item.Recipe,
            GaugeMultiplier = item.GaugeMultiplier,
            Burnt = item.Burnt,
            Kind = c.Kind,
            RacePriceWeight = Customer.PriceWeightOf(c.Kind),
            BasePrice = Menus.BasePriceOf(item.Menu),
        });

        // 탄 것을 팔면 매장 전체 분위기가 나빠진다 (기획서 5.3).
        if (item.Burnt)
            foreach (var w in waiting)
                if (w != null) w.AddPatienceServer(-burntPatiencePenalty);

        Cafe.Of(this)?.Dishes?.SoilServer();
        if (c.CountServedServer()) Leave(index);
        return true;
    }

}
