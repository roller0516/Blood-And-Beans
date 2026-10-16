using UnityEngine;

/// 손님 모델의 애니메이션. 걷는지는 복제된 위치 변화로, 떠나는 몸짓은 서버가 정한 이탈 이유로 정한다.
[DisallowMultipleComponent]
public sealed class CustomerMotion : MonoBehaviour
{
    // AC_Customer_Ghost_001.controller의 상태 이름. 컨트롤러를 고치면 여기도 같이 고친다.
    static readonly int IdleHash = Animator.StringToHash("Idle");
    static readonly int MoveHash = Animator.StringToHash("Move");
    static readonly int ReceiptHash = Animator.StringToHash("Receipt");
    static readonly int LeaveDisappointmentHash = Animator.StringToHash("LeaveDisappointment");
    static readonly int LeaveAngerHash = Animator.StringToHash("LeaveAnger");

    // Customer_Ghost_001.fbx 눈 메시의 대기 표정 블렌드 쉐입. 인내심 단계 순서다 (낮 피드백 1장 EX-01~03).
    static readonly string[] WaitingShapes = { "EX01_Calm", "EX02_Anxious", "EX03_Angry" };
    const float FullWeight = 100f;   // Unity 블렌드 쉐입 가중치 상한

    [SerializeField] Animator animator;
    [SerializeField] Customer customer;
    [Tooltip("이 속도(m/s)보다 빠르면 걷는 것으로 본다.")]
    [SerializeField, Min(0f)] float walkThreshold = 0.1f;
    [SerializeField, Min(0f)] float crossFadeSeconds = 0.15f;
    [SerializeField] SkinnedMeshRenderer eyes;
    [Tooltip("대기 표정이 다음 단계로 넘어가는 시간(초).")]
    [SerializeField, Min(0.01f)] float expressionSeconds = 0.2f;

    readonly int[] shapeIndex = new int[WaitingShapes.Length];
    readonly float[] shapeWeight = new float[WaitingShapes.Length];

    Vector3 lastPosition;
    int state = -1;
    bool leaving;

    void Awake()
    {
        if (animator == null || customer == null || eyes == null)
        {
            CDebug.LogError($"{name}: Animator·Customer·눈 렌더러가 연결되지 않았다. 손님 애니메이션이 재생되지 않는다.", this);
            enabled = false;
            return;
        }

        for (var i = 0; i < WaitingShapes.Length; i++)
        {
            shapeIndex[i] = eyes.sharedMesh.GetBlendShapeIndex(WaitingShapes[i]);
            if (shapeIndex[i] >= 0) continue;
            CDebug.LogError($"{name}: 눈 메시에 블렌드 쉐입 {WaitingShapes[i]}가 없다.", this);
            enabled = false;
        }
    }

    void OnEnable()
    {
        lastPosition = transform.position;
        // 손님은 NetworkPool로 재사용된다. 지난 손님의 이탈 상태와 표정을 지운다.
        leaving = false;
        state = -1;
        for (var i = 0; i < shapeWeight.Length; i++) shapeWeight[i] = i == 0 ? FullWeight : 0f;
        customer.ExitChanged += OnExit;
    }

    void OnDisable() => customer.ExitChanged -= OnExit;

    /// 떠나는 몸짓은 한 번 재생하고 끝까지 둔다. 서버가 시간이 되면 디스폰한다.
    void OnExit(CustomerExit reason)
    {
        var next = reason switch
        {
            CustomerExit.Served => ReceiptHash,
            CustomerExit.Impatient => LeaveDisappointmentHash,
            CustomerExit.Angry => LeaveAngerHash,
            _ => 0,
        };
        if (next == 0) return;

        leaving = true;
        state = next;
        animator.CrossFadeInFixedTime(next, crossFadeSeconds);
    }

    void LateUpdate()
    {
        if (leaving) return;

        var dt = Time.deltaTime;
        if (dt <= 0f) return;

        ShowPatience(dt);

        var position = transform.position;
        var speed = (position - lastPosition).magnitude / dt;
        lastPosition = position;

        var next = speed > walkThreshold ? MoveHash : IdleHash;
        if (next == state) return;

        state = next;
        animator.CrossFadeInFixedTime(next, crossFadeSeconds);
    }

    /// 대기·이동 클립이 여유 표정을 매 프레임 덮어써서, 애니메이터가 돈 뒤(LateUpdate)에 다시 쓴다.
    /// 경계는 말풍선 채움 색과 같다 (기획서 5.7.2). 이탈 표정은 이탈 클립이 갖고 있다.
    void ShowPatience(float dt)
    {
        var ratio = customer.PatienceRatio;
        var stage = ratio <= DayBalance.PatienceUrgent ? 2 : ratio <= DayBalance.PatienceWarning ? 1 : 0;
        var step = FullWeight * dt / expressionSeconds;
        for (var i = 0; i < shapeWeight.Length; i++)
        {
            shapeWeight[i] = Mathf.MoveTowards(shapeWeight[i], i == stage ? FullWeight : 0f, step);
            eyes.SetBlendShapeWeight(shapeIndex[i], shapeWeight[i]);
        }
    }
}
