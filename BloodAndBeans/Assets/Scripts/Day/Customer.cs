using Unity.Netcode;
using UnityEngine;

/// 대기 중인 손님 한 명 (기획서 5.5). 선호 태그와 줄어드는 인내심 게이지를 들고 있다.
/// 인내심이 바닥나면 나가 버리고 그 주문은 아무 수익도 남기지 않는다.
public class Customer : NetworkBehaviour
{
    readonly NetworkVariable<Race> race = new();
    readonly NetworkVariable<MenuTag> require = new();   // 이 태그는 전부 있어야 한다
    readonly NetworkVariable<MenuTag> anyOf = new();     // 설정된 경우 이 중 최소 하나
    readonly NetworkVariable<int> minIngredients = new();
    readonly NetworkVariable<int> orderCount = new();
    readonly NetworkVariable<int> served = new();
    readonly NetworkVariable<float> patience = new();
    readonly NetworkVariable<int> team = new(-1);

    public Race Kind => race.Value;
    public int TeamId => team.Value;

    /// 대기열은 손님을 먼저 스폰하고 종족은 잠시 뒤에 배정한다. 그래서 표현 쪽은
    /// 스폰이 아니라 값의 변화를 따라가야 한다.
    public event System.Action<Race> RaceChanged;
    public MenuTag Require => require.Value;
    public MenuTag AnyOf => anyOf.Value;
    public int MinIngredients => minIngredients.Value;
    public int Remaining => orderCount.Value - served.Value;
    public float Patience => patience.Value;
    public float PatienceRatio => patience.Value / PatienceOf(race.Value);

    // ponytail: 임시값이다. 기획서 14장이 인내심 길이와 가격 폭을 열어 뒀고, 상대적인
    // 순서(좀비는 길고 싸다, 뱀파이어는 짧고 비싸다, 마녀가 가장 비싸다)만 확정돼 있다.
    // 표가 생기면 DT_Passive/DT_Menu로 옮긴다.
    public static float PatienceOf(Race s) => s switch
    {
        Race.Zombie => 90f,
        Race.Vampire => 30f,
        _ => 60f,
    };

    /// 종족별 가격 가중치. 실제 공식은 Economy가 소유하고, 이 값은 그 입력 중 하나다.
    public static float PriceWeightOf(Race s) => s switch
    {
        Race.Zombie => 0.7f,
        Race.Vampire => 1.4f,
        Race.Witch => 1.8f,
        _ => 1.0f,
    };

    public override void OnNetworkSpawn()
    {
        race.OnValueChanged += (_, now) => RaceChanged?.Invoke(now);
        RaceChanged?.Invoke(race.Value);
    }

    public void SetupServer(int teamId, Race s, MenuTag req, MenuTag any, int minParts, int count)
    {
        if (!IsServer) return;
        team.Value = teamId;
        var was = race.Value;
        race.Value = s;
        if (IsServer && was == s) RaceChanged?.Invoke(s);   // 같은 값을 쓰면 변경 이벤트가 발생하지 않는다
        require.Value = req;
        anyOf.Value = any;
        minIngredients.Value = minParts;
        orderCount.Value = count;
        served.Value = 0;
        patience.Value = PatienceOf(s);
    }

    void Update()
    {
        if (!IsServer || patience.Value <= 0f) return;
        patience.Value = Mathf.Max(0f, patience.Value - Time.deltaTime);
    }

    /// 태그로만 대조한다. 손님은 메뉴 이름을 알지 못한다 (기획서 7.2).
    public bool Accepts(MenuTag tags, int ingredientCount) =>
        (require.Value & tags) == require.Value &&
        (anyOf.Value == MenuTag.None || (anyOf.Value & tags) != MenuTag.None) &&
        ingredientCount >= minIngredients.Value;

    /// 손님이 원하던 마지막 항목이었으면 true를 돌려준다.
    public bool CountServedServer()
    {
        if (!IsServer) return false;
        served.Value++;
        return Remaining <= 0;
    }

    public void AddPatienceServer(float delta)
    {
        if (!IsServer) return;
        patience.Value = Mathf.Clamp(patience.Value + delta, 0f, PatienceOf(race.Value));
    }
}
