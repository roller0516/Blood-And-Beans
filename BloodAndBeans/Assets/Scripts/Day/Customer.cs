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

    /// 이 손님의 인내심 최대치. 종족표(`PatienceOf`)에 「인기 카페」 배수가 곱해진
    /// 결과라, 게이지 비율을 그릴 때 종족표를 다시 읽으면 안 된다 (기획서 9.1).
    readonly NetworkVariable<float> patienceMax = new(1f);

    /// 인내심이 닳지 않는 손님인가 (기획서 9.1 「붙임성」: 매장의 첫 손님).
    readonly NetworkVariable<bool> patient = new();

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

    /// 이 손님의 인내심 최대치. 「인기 카페」가 걸려 있으면 종족표보다 크다.
    public float PatienceMax => patienceMax.Value > 0f ? patienceMax.Value : PatienceOf(race.Value);

    public float PatienceRatio => patience.Value / PatienceMax;

    /// 「붙임성」이 걸린 첫 손님인가 (기획서 9.1).
    public bool Patient => patient.Value;

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

    /// `patienceScale`은 「인기 카페」 배수이고 `neverImpatient`는 「붙임성」이다
    /// (기획서 9.1). 둘 다 대기열이 팀 패시브를 보고 넘긴다 — 손님이 스스로 팀을 뒤지면
    /// 손님 수만큼 순회가 늘어난다.
    public void SetupServer(int teamId, Race s, MenuTag req, MenuTag any, int minParts, int count,
                            float patienceScale = 1f, bool neverImpatient = false)
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

        patienceMax.Value = PatienceOf(s) * Mathf.Max(0.01f, patienceScale);
        patience.Value = patienceMax.Value;
        patient.Value = neverImpatient;
    }

    void Update()
    {
        if (!IsServer || patience.Value <= 0f) return;

        // 「붙임성」이 걸린 손님은 게이지가 줄지 않는다 (기획서 9.1).
        if (patient.Value) return;

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
        patience.Value = Mathf.Clamp(patience.Value + delta, 0f, PatienceMax);
    }
}
