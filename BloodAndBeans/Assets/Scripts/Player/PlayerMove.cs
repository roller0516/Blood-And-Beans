using Unity.Netcode;
using UnityEngine;

/// 소유자는 크기가 제한된 입력 의도만 보내고, transform의 권위는 서버가 가진다.
///
/// 소유자 클라이언트는 같은 이동식을 로컬에서 먼저 돌린다(예측). 서버 결과와의 차이는
/// PlayerPrediction이 화해로 메우고, PlayerNetworkTransform이 그동안 권위 위치가 예측을
/// 덮어쓰지 않게 막는다. 예측이 없으면 입력에서 화면까지 왕복 지연이 그대로 보인다.
/// 판정에 쓰이는 위치는 여전히 서버 것 하나뿐이다.
[RequireComponent(typeof(CharacterController))]
public class PlayerMove : NetworkBehaviour
{
    [SerializeField] float speed = 5f;

    /// 서버가 쓰고 소유자가 읽는다. 무게 밴드는 TeamLedger에서 나오는데 그 원장은 서버
    /// 전용이라(MatchDirector.LedgerOf) 클라이언트가 스스로 계산할 수 없다. 소유자가 다른
    /// 속도로 예측하면 매 프레임 어긋나 화해가 위치를 계속 당긴다.
    readonly NetworkVariable<float> speedScale = new(1f,
        NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

    CharacterController controller;
    PlayerInventory inventory;
    DashHarass harass;

    Vector2 serverInput;                         // 서버가 RPC로 받은 값
    Vector2 predictedInput;                      // 소유자가 예측에 쓰는 값
    Vector3 facing = Vector3.forward;

    /// 마지막으로 움직인 방향. 아무것도 플레이어를 회전시키지 않으므로 transform.forward는
    /// 항상 월드 +Z다. 대시 돌진처럼 "바라보는 쪽"이 필요한 서버 판정은 여기를 쓴다.
    public Vector3 FacingServer => facing;

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        inventory = GetComponent<PlayerInventory>();
        harass = GetComponent<DashHarass>();
    }

    void Update()
    {
        if (IsServer)
        {
            // 대시 돌진과 넉백은 LateUpdate에서 위치를 덮어쓴다. 여기서 같이 밀면 두 이동이
            // 겹쳐 돌진 거리가 입력만큼 늘거나 줄어든다.
            if (harass != null && harass.SuppressesInputServer) return;

            var load = inventory != null ? inventory.CurrentSpeedMultiplier : 1f;

            // 밴드 값이라 실제로는 거의 바뀌지 않는다. 같은 값을 다시 쓰지 않으려는 비교다.
            if (!Mathf.Approximately(speedScale.Value, load)) speedScale.Value = load;

            StepMove(serverInput, load);
            return;
        }

        if (IsOwner) StepMove(predictedInput, speedScale.Value);
    }

    /// 서버와 소유자가 반드시 같은 식을 쓴다. 둘이 갈라지면 화해가 매 프레임 위치를 당겨
    /// 그 자체가 떨림이 된다.
    ///
    /// transform 대입이 아니라 CharacterController다. 대입은 벽과 설비를 그냥 통과했다.
    /// y는 건드리지 않는다. 평면 탑다운이라 중력도 접지 처리도 쓰지 않는다.
    void StepMove(Vector2 input, float load) =>
        controller.Move(new Vector3(input.x, 0f, input.y) * (speed * load * Time.deltaTime));

    public void SetInputClient(Vector2 input)
    {
        if (!IsOwner) return;

        var clamped = Vector2.ClampMagnitude(input, 1f);
        predictedInput = clamped;
        SetInputRpc(clamped);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    void SetInputRpc(Vector2 input)
    {
        serverInput = Vector2.ClampMagnitude(input, 1f);

        // 입력을 놓는 순간(0,0)에는 갱신하지 않는다. 멈춰 서면 마지막 방향을 그대로 본다.
        if (serverInput.sqrMagnitude > 0.0001f)
            facing = new Vector3(serverInput.x, 0f, serverInput.y).normalized;
    }
}
