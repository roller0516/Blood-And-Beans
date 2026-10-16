using Unity.Netcode;
using UnityEngine;

/// 손님이 떠나는 이유. 이유마다 몸짓이 다르다 (18장 6).
public enum CustomerExit
{
    None,
    Served,       // 주문을 다 받았다
    Impatient,    // 인내심이 바닥났다
    Angry,        // 오배송으로 화나서 떠난다
}

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

    readonly NetworkVariable<int> queueIndex = new();
    public int QueueIndex => queueIndex.Value;
    public void SetQueueIndexServer(int index) { if (IsServer) queueIndex.Value = index; }
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

    /// 종족별 인내심(초). 기획서 5.5 표.
    public static float PatienceOf(Race s) => s switch
    {
        Race.Zombie => 70f,
        Race.Ghost => 45f,
        Race.Skeleton => 45f,
        Race.Werewolf => 55f,
        Race.Vampire => 28f,
        Race.Witch => 50f,
        _ => 50f,
    };

    /// 종족별 가격 가중치. 실제 공식은 Economy가 소유하고, 이 값은 그 입력 중 하나다.
    /// 기획서 5.5 표.
    public static float PriceWeightOf(Race s) => s switch
    {
        Race.Zombie => 0.8f,
        Race.Werewolf => 1.1f,
        Race.Vampire => 1.3f,
        Race.Witch => 1.45f,
        _ => 1.0f,
    };

    readonly NetworkVariable<CustomerExit> exit = new();
    public CustomerExit Exit => exit.Value;

    /// 떠나는 몸짓을 재생할 표현 쪽이 구독한다. 몸짓이 끝나면 서버가 스스로 디스폰한다.
    public event System.Action<CustomerExit> ExitChanged;

    public override void OnNetworkSpawn()
    {
        race.OnValueChanged += (_, now) => RaceChanged?.Invoke(now);
        RaceChanged?.Invoke(race.Value);
        exit.OnValueChanged += (_, now) => ExitChanged?.Invoke(now);
    }

    /// `patienceScale`은 「찻잎」 보석 배수다 (기획서 8.2). 대기열이 팀 상태를 보고 넘긴다 —
    /// 손님이 스스로 팀을 뒤지면 손님 수만큼 순회가 늘어난다.
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

    [Tooltip("입장할 때 걷는 속도(m/s).")]
    [SerializeField, Min(0.01f)] float walkSpeed = 2.5f;
    [Tooltip("떠나는 데 걸리는 시간(초). 입구까지 이 시간에 걸어 가장 긴 이탈 클립(receipt 3.0초)이 끝날 즈음 닿는다.")]
    [SerializeField, Min(0.1f)] float exitSeconds = 3f;

    Vector3 goal;
    Quaternion goalFacing;
    float speed;
    bool walking;
    float exitTimer;

    /// 대기열에서 빠진 손님이 이탈 몸짓을 보이며 `exitPoint`(입구)로 걸어 나간다. 닿으면 디스폰한다.
    public void ExitServer(CustomerExit reason, Vector3 exitPoint)
    {
        if (!IsServer || reason == CustomerExit.None || exit.Value != CustomerExit.None) return;
        goal = exitPoint;
        goalFacing = transform.rotation;
        speed = Vector3.Distance(transform.position, exitPoint) / exitSeconds;
        walking = true;
        exitTimer = exitSeconds;
        exit.Value = reason;
    }

    /// 자리까지 걸어간다. 도착하면 `facing`을 본다. 위치는 NetworkTransform이 모두에게 복제한다.
    public void WalkToServer(Vector3 position, Quaternion facing)
    {
        if (!IsServer) return;
        goal = position;
        goalFacing = facing;
        speed = walkSpeed;
        walking = true;
    }

    void Update()
    {
        if (!IsServer) return;
        if (walking) Walk();

        if (exit.Value != CustomerExit.None)
        {
            exitTimer -= Time.deltaTime;
            if (!walking && exitTimer <= 0f && NetworkObject.IsSpawned) NetworkObject.Despawn();
            return;
        }

        // 걷는 동안에는 인내심이 닳지 않는다. 자리에 선 뒤부터 줄어든다.
        if (walking) return;

        if (patience.Value <= 0f) return;

        // 「붙임성」이 걸린 손님은 게이지가 줄지 않는다 (기획서 9.1).
        if (patient.Value) return;

        patience.Value = Mathf.Max(0f, patience.Value - Time.deltaTime);
    }

    void Walk()
    {
        var from = transform.position;
        var to = Vector3.MoveTowards(from, goal, speed * Time.deltaTime);
        transform.position = to;

        walking = to != goal;
        if (!walking) { transform.rotation = goalFacing; return; }

        var heading = new Vector3(to.x - from.x, 0f, to.z - from.z);
        if (heading.sqrMagnitude > 0f) transform.rotation = Quaternion.LookRotation(heading);
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
