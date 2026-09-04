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

    /// 캐릭터가 가는 쪽으로 도는 속도(초당 도). 3인칭 카메라가 되면서 캐릭터의 앞이
    /// 화면에서 읽히게 됐다 - 예전 고정 탑다운에서는 아무도 회전을 보지 않았다.
    [SerializeField] float turnDegreesPerSecond = 720f;

    /// 서버가 쓰고 소유자가 읽는다. 무게 밴드는 TeamLedger에서 나오는데 그 원장은 서버
    /// 전용이라(MatchDirector.LedgerOf) 클라이언트가 스스로 계산할 수 없다. 소유자가 다른
    /// 속도로 예측하면 매 프레임 어긋나 화해가 위치를 계속 당긴다.
    readonly NetworkVariable<float> speedScale = new(1f,
        NetworkVariableReadPermission.Owner, NetworkVariableWritePermission.Server);

    CharacterController controller;

    /// 이 시각까지는 조작 입력을 무시한다. 대시 돌진·넉백처럼 위치를 직접 미는 기능이
    /// 여기를 올린다. 이동은 누가 올렸는지 묻지 않는다 — 그래서 상태이상이 늘어도
    /// 이 파일은 그대로다.
    ///
    /// 구간은 늘어나기만 하고 줄지 않는다(`Mathf.Max`). 둘이 겹쳐 걸렸을 때 먼저 끝난
    /// 쪽이 남은 구간까지 풀어 버리면 안 되기 때문이다.
    float suppressedUntil;

    Vector2 serverInput;                         // 서버가 RPC로 받은 값
    Vector2 predictedInput;                      // 소유자가 예측에 쓰는 값
    Vector3 facing = Vector3.forward;

    /// 발이 지면에 닿는 높이. 스폰 위치의 y가 곧 그 높이다
    /// (MatchDirector.NightSpawnPosition / CafeSpawnPosition 둘 다 spawnHeight로 띄운다).
    float groundedY;

    /// 마지막으로 움직인 방향. 아무것도 플레이어를 회전시키지 않으므로 transform.forward는
    /// 항상 월드 +Z다. 대시 돌진처럼 "바라보는 쪽"이 필요한 서버 판정은 여기를 쓴다.
    public Vector3 FacingServer => facing;

    /// 서버가 관측한 "지금 움직이고 있는가". 파밍 캔슬(이동하면 상자 창이 닫힘)의 판단
    /// 근거다. 클라이언트가 "안 움직였다"고 말하게 두면 그 규칙이 없는 것과 같다.
    public bool MovingServer => IsServer && serverInput.sqrMagnitude > 0.0001f;

    /// 접지 높이를 스폰 시점에 한 번 잡는다. 서버와 소유자가 같은 값을 잡아야
    /// 예측 화해가 y를 계속 당기지 않는다.
    ///
    /// **스폰 y에 skinWidth를 더한다.** 스폰 높이는 캡슐 바닥을 지면에 정확히 맞추는데,
    /// CharacterController는 그 자리를 "지면에 박힌 상태"로 본다 — 항상 skinWidth만큼
    /// 떠 있으려 하기 때문이다. 박힌 채로 `Move`를 부르면 수평 이동이 통째로 먹히고
    /// (collisionFlags가 Sides로 온다) 대신 위로 밀려난다. 그것을 `PinToGround`가 매
    /// 프레임 도로 끌어내리므로, 캐릭터는 제자리에서 위아래로 떨기만 하고 걷지 못한다.
    /// 8cm 띄워 두면 그 싸움 자체가 없어진다.
    public override void OnNetworkSpawn() =>
        groundedY = transform.position.y + controller.skinWidth;

    void Awake() => controller = GetComponent<CharacterController>();

    /// 위치를 직접 미는 기능이 그 구간 동안 조작을 죽인다. 서버만 부른다.
    ///
    /// 대시 돌진과 넉백은 LateUpdate에서 위치를 덮어쓴다. 그 사이 여기서 같이 밀면
    /// 두 이동이 겹쳐 돌진 거리가 입력만큼 늘거나 줄어든다.
    public void SuppressInputUntilServer(float endTime)
    {
        if (!IsServer) return;
        suppressedUntil = Mathf.Max(suppressedUntil, endTime);
    }

    /// 이동 속도 배수를 정한다. 서버만 쓴다.
    ///
    /// 무게 밴드는 서버 전용 원장에서 나오므로(`MatchDirector.LedgerOf`) 소유자가 스스로
    /// 계산할 수 없다. 여기 넣은 값이 복제되어 소유자의 예측이 서버와 같은 속도를 쓴다.
    public void SetSpeedScaleServer(float value)
    {
        if (!IsServer) return;

        loadScale = value;
        PushScaleServer();
    }

    /// 캐릭터 낮 패시브에서 나오는 배수 (기획서 9.1의 잰걸음·강심장). 서버만 쓴다.
    ///
    /// 무게 배수와 채널을 나눈 이유는 둘이 서로 다른 이유로, 서로 다른 시점에 바뀌기
    /// 때문이다. 한 값에 섞으면 무게가 바뀔 때마다 캐릭터를 다시 묻고, 큐가 바뀔 때마다
    /// 가방을 다시 물어야 한다.
    public void SetPassiveScaleServer(float value)
    {
        if (!IsServer) return;

        passiveScale = value;
        PushScaleServer();
    }

    /// 두 채널을 곱해 실제 배수를 낸다.
    void PushScaleServer()
    {
        var want = loadScale * passiveScale;

        // 밴드 값이라 실제로는 거의 바뀌지 않는다. 같은 값을 다시 쓰지 않으려는 비교다.
        if (!Mathf.Approximately(speedScale.Value, want)) speedScale.Value = want;
    }

    /// 무게에서 오는 배수 (`PlayerInventory`)와 캐릭터에서 오는 배수 (`PlayerCharacter`).
    float loadScale = 1f;
    float passiveScale = 1f;

    /// 지금 적용 중인 속도 배수. 서버가 정하고 소유자에게 복제된 값이라, 소유자 화면이
    /// 서버와 다른 숫자를 보여 주지 않는다.
    public float SpeedScale => speedScale.Value;

    void Update()
    {
        if (IsServer)
        {
            if (Time.time < suppressedUntil) return;

            StepMove(serverInput, speedScale.Value);
            return;
        }

        if (IsOwner) StepMove(predictedInput, speedScale.Value);
    }

    /// 서버와 소유자가 반드시 같은 식을 쓴다. 둘이 갈라지면 화해가 매 프레임 위치를 당겨
    /// 그 자체가 떨림이 된다.
    ///
    /// transform 대입이 아니라 CharacterController다. 대입은 벽과 설비를 그냥 통과했다.
    /// 이동 벡터의 y는 항상 0이지만 컨트롤러가 겹침을 풀며 y를 바꾸므로 PinToGround로
    /// 되돌린다. 평면이라 중력도 접지 처리도 쓰지 않는다.
    void StepMove(Vector2 input, float load)
    {
        var direction = new Vector3(input.x, 0f, input.y);
        controller.Move(direction * (speed * load * Time.deltaTime));
        PinToGround();

        // 회전은 이동과 무관하다. 입력이 이미 월드 방향이라 서버와 소유자가 같은 결과를
        // 얻고, 회전이 이동식에 끼어들지 않으므로 예측 화해도 흔들리지 않는다.
        if (direction.sqrMagnitude <= 0.0001f) return;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            Quaternion.LookRotation(direction.normalized, Vector3.up),
            turnDegreesPerSecond * Time.deltaTime);
    }

    /// 평면 탑다운이라 y는 상수다. 그런데 CharacterController는 겹침을 풀 때 수평만
    /// 밀어내지 않는다 — 스폰하면서 지면에 맞닿거나 상자·설비·다른 플레이어와 겹치면
    /// 위아래로도 밀어낸다. 중력이 없어서 그 오차를 되돌릴 힘이 없으므로 한 번 뜨거나
    /// 박히면 영구히 남는다. 그래서 이동을 마칠 때마다 접지 높이로 되돌린다.
    ///
    /// 컨트롤러를 잠깐 꺼야 대입이 남는다. 켜진 채로 대입하면 컨트롤러가 자기 내부
    /// 위치를 다시 써 넣는다 (PlayerTeleport, PlayerPrediction.SnapTo와 같은 이유).
    void PinToGround()
    {
        var position = transform.position;
        if (Mathf.Approximately(position.y, groundedY)) return;

        controller.enabled = false;
        transform.position = new Vector3(position.x, groundedY, position.z);
        controller.enabled = true;
    }

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
