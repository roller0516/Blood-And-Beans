using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

/// 플레이어 한 명의 이동·몸체 회전·상호작용 진입점. 서버 권위 + 소유자 예측으로 움직이고,
/// F로 하는 모든 행동(대상 탐색·상자 홀드)이 여기를 거친다.
///
/// **예전에는 `PlayerMove`(이동)와 `PlayerInteractor`(상호작용)로 나뉘어 있었다.** 둘 다
/// 같은 루트 GameObject의 NetworkBehaviour고, `PlayerInteractor`는 직렬화 필드가 하나도
/// 없어(전부 런타임 상태) 나누는 대가만 있고 얻는 것이 없었다 — 입력 라우터
/// (`PlayerInputRouter`)가 "이 플레이어가 지금 할 수 있는 일"을 물을 자리가 둘로 갈렸고,
/// 새 행동을 추가할 때마다 이동/상호작용 어느 컴포넌트에 넣을지부터 판단해야 했다.
/// 소유자 예측(`PlayerPrediction`)·복제(`PlayerNetworkTransform`)와 인벤토리·능력·팀
/// (`PlayerInventory`·`PlayerAbilities`·`PlayerTeam`)은 각자 독립된 복제 상태와 NGO 수명이
/// 필요해 그대로 남긴다 — 합친 것은 "몸이 하는 일" 둘뿐이다.
///
/// 이 파일은 예전 `PlayerMove.cs`의 GUID를 그대로 쓴다(스크립트 리네임). 그래야 프리팹의
/// 이동 관련 직렬화 값(속도·회전 속도)이 그대로 남는다 — `PlayerInteractor`는 직렬화 필드가
/// 없어 그쪽 컴포넌트는 값 손실 없이 지울 수 있었다.
///
/// 소유자 클라이언트는 같은 이동식을 로컬에서 먼저 돌린다(예측). 서버 결과와의 차이는
/// PlayerPrediction이 화해로 메우고, PlayerNetworkTransform이 그동안 권위 위치가 예측을
/// 덮어쓰지 않게 막는다. 예측이 없으면 입력에서 화면까지 왕복 지연이 그대로 보인다.
/// 판정에 쓰이는 위치는 여전히 서버 것 하나뿐이다.
[RequireComponent(typeof(CharacterController))]
public class PlayerController : NetworkBehaviour
{
    // ============================================================
    // 이동·몸체 회전 (예전 PlayerMove)
    // ============================================================

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
    PlayerTeam playerTeam;
    GamePhase phase;

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
    public override void OnNetworkSpawn()
    {
        groundedY = transform.position.y + controller.skinWidth;
        MatchDirector.Bind(BindDirector);
    }

    void BindDirector(MatchDirector director) => phase = director != null ? director.Phase : null;

    public override void OnNetworkDespawn()
    {
        MatchDirector.Unbind(BindDirector);
        phase = null;

        if (IsServer) ReleaseBoxServer();

        // 디스폰(재사용 포함)될 때 다음 스폰이 지난 판의 후보·캐시·상자 세션을 물려받지
        // 않게 전부 비운다. 그대로 두면 재스폰 첫 프레임에 이미 사라진 대상으로 안내하거나
        // RPC를 보낸다.
        candidates.Clear();
        candidateRefs.Clear();
        candidateTeams.Clear();
        current = null;
        cachedTarget = null;
        Latest = null;
        boxHeld = null;
    }

    void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerTeam = GetComponent<PlayerTeam>();
        localCarry = GetComponent<PlayerCarry>();
    }

    /// 접지 높이를 이 y 기준으로 다시 잡는다. `groundLevelY`는 **캡슐 바닥이 지면에 닿는**
    /// y이며, `OnNetworkSpawn`과 같은 식으로 skinWidth만큼 띄운 값이 접지 높이가 된다.
    ///
    /// 텔레포트는 y를 바꾸는데 `groundedY`는 스폰 시점 값으로 굳어 있다. 갱신하지 않으면
    /// `PinToGround`가 매 프레임 옛 높이로 끌어내려 캡슐이 지면에 박히고, 박힌 채로는
    /// 수평 이동이 통째로 먹힌다 — `OnNetworkSpawn` 주석이 설명한 그 상태다.
    public void RebaseGroundServer(float groundLevelY)
    {
        if (!IsServer) return;
        groundedY = groundLevelY + controller.skinWidth;
    }

    /// 서버가 이미 띄워 둔 y를 소유자가 그대로 받는다 (`PlayerPrediction.SnapTo`).
    /// 여기서 skinWidth를 또 더하면 화해할 때마다 8cm씩 떠오른다.
    public void AdoptGroundedOwner(float authoritativeY)
    {
        if (!IsOwner) return;
        groundedY = authoritativeY;
    }

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
        // 상호작용 후보 캐시 (예전 PlayerInteractor.Update). 손 상태·파괴·디스폰은 프레임마다
        // 바뀔 수 있으므로 매 프레임 다시 훑는다 — 이동 게이트와 무관하게 항상 돈다.
        if (IsOwner) cachedTarget = Nearest();

        UpdateMovement();
    }

    void UpdateMovement()
    {
        if (playerTeam != null && playerTeam.IsTeleporting)
        { serverInput = Vector2.zero; predictedInput = Vector2.zero; return; }
        if (phase != null && (!phase.Started || phase.Finished || phase.Current == Phase.Transition))
        { serverInput = Vector2.zero; predictedInput = Vector2.zero; return; }
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
    /// **`controller.Move`를 부르는 곳은 직후에 반드시 이것을 부른다.** 예전에는
    /// `StepMove`만 불렀고, LateUpdate에서 미는 예측 화해(`PlayerPrediction`)와 돌진·넉백
    /// (`DashHarass`)이 올린 y는 되돌리는 곳이 없었다 — 카메라가 그 y를 그대로 읽어서
    /// 걸을 때마다 위아래로 떨었고, 벽·나무에 붙어 교정이 커질수록 심해졌다.
    ///
    /// 컨트롤러를 잠깐 꺼야 대입이 남는다. 켜진 채로 대입하면 컨트롤러가 자기 내부
    /// 위치를 다시 써 넣는다 (PlayerTeleport, PlayerPrediction.SnapTo와 같은 이유).
    public void PinToGround()
    {
        var position = transform.position;
        if (Mathf.Approximately(position.y, groundedY)) return;

        controller.enabled = false;
        transform.position = new Vector3(position.x, groundedY, position.z);
        controller.enabled = true;
    }

    // 소유자 입력도 비유한 값은 허용하지 않는다.
    public static Vector2 SanitizeInput(Vector2 input) =>
        float.IsNaN(input.x) || float.IsInfinity(input.x) || float.IsNaN(input.y) || float.IsInfinity(input.y)
            ? Vector2.zero : Vector2.ClampMagnitude(input, 1f);

    public void SetInputClient(Vector2 input)
    {
        if (!IsOwner) return;

        var clamped = SanitizeInput(input);
        predictedInput = clamped;
        SetInputRpc(clamped);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    void SetInputRpc(Vector2 input)
    {
        serverInput = SanitizeInput(input);

        // 입력을 놓는 순간(0,0)에는 갱신하지 않는다. 멈춰 서면 마지막 방향을 그대로 본다.
        if (serverInput.sqrMagnitude > 0.0001f)
            facing = new Vector3(serverInput.x, 0f, serverInput.y).normalized;
    }

    // ============================================================
    // 상호작용 (예전 PlayerInteractor)
    // ============================================================

    public event System.Action<Vector3> InteractionSucceeded;
    public event System.Action<SfxCue, Vector3, int> SoundPlayed;

    /// 서버가 확정한 팀 사건만 보낸다. 카페 내부 정보는 다른 팀에 전달하지 않는다 (3.4).
    public static void ReportSoundServer(ulong clientId, Component target, SfxCue cue)
    {
        var manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer || target == null || cue == SfxCue.None ||
            !manager.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject == null) return;
        var player = client.PlayerObject.GetComponent<PlayerController>();
        var team = PlayerTeam.Of(clientId);
        if (player == null || !player.IsSpawned || team < 0) return;
        foreach (var receiver in manager.ConnectedClientsList)
            if (PlayerTeam.Of(receiver.ClientId) == team)
                player.SoundRpc(cue, target.transform.position, team, player.RpcTarget.Single(receiver.ClientId, RpcTargetUse.Temp));
    }

    [Rpc(SendTo.SpecifiedInParams, InvokePermission = RpcInvokePermission.Server)]
    void SoundRpc(SfxCue cue, Vector3 position, int team, RpcParams p = default) =>
        SoundPlayed?.Invoke(cue, position, team);

    /// 성공 결과를 반영한 서버만 호출한다. 피드백은 행동한 플레이어에게만 전달한다.
    public static void ReportSuccessServer(ulong clientId, Component target)
    {
        var manager = NetworkManager.Singleton;
        if (manager == null || !manager.IsServer || target == null ||
            !manager.ConnectedClients.TryGetValue(clientId, out var client) || client.PlayerObject == null) return;
        var interaction = client.PlayerObject.GetComponent<PlayerController>();
        if (interaction == null || !interaction.IsSpawned) return;
        Collider surface = target.GetComponent<CharacterController>();
        if (surface == null) surface = target.GetComponentInChildren<Collider>();
        var position = target.transform.position;
        if (surface != null)
        {
            // 긴 서빙대도 조작한 쪽 가장자리 위에 뜬다.
            position = surface.bounds.ClosestPoint(client.PlayerObject.transform.position);
            position.y = surface.bounds.max.y;
        }
        interaction.SuccessRpc(position);
    }

    [Rpc(SendTo.Owner, InvokePermission = RpcInvokePermission.Server)]
    void SuccessRpc(Vector3 position) => InteractionSucceeded?.Invoke(position);

    readonly List<IInteractable> candidates = new();

    /// 대상 하나에 콜라이더가 여럿 겹칠 수 있다(설비 부품마다 따로인 경우). 참조 수를 세어
    /// 마지막 콜라이더가 빠질 때만 후보에서 지운다 — 하나만으로 지우면 다른 콜라이더가
    /// 아직 겹쳐 있어도 후보가 사라진다.
    readonly Dictionary<IInteractable, int> candidateRefs = new();

    /// 후보가 `PlayerCarry`일 때만 채운다. 등록 시점(사건 한 번)에 한 번만 찾아 두면
    /// `Nearest()`가 매 프레임 `PlayerTeam.Of`로 다시 찾지 않는다 (AGENTS.md 참조와 결합도).
    readonly Dictionary<IInteractable, PlayerTeam> candidateTeams = new();

    IInteractable current;

    /// 이번 프레임의 최근접 대상. `Target`과 `Prompt`가 이 값 하나를 같이 읽는다 —
    /// 예전에는 둘 다 `Nearest()`를 따로 불러서 HUD 한 갱신에서만 두 번 돌았고
    /// (`MatchHudPresenter.BuildModel`이 `interactor.Prompt`를 두 번 읽었다), `TargetOutline`은
    /// 또 매 프레임 자기 몫을 따로 돌았다. 여기서 프레임당 한 번만 훑어 셋이 같은
    /// 결과를 보게 한다.
    IInteractable cachedTarget;
    PlayerCarry localCarry;

    /// 지금 손 상태를 대상에게 넘기는 입력. `CanPromptClient`·`PromptFor`가 같이 받는다.
    InteractionContext Context => new(
        localCarry != null ? localCarry.View : CarryView.Nothing,
        localCarry != null && localCarry.Reserved);

    /// 인터페이스 참조는 Unity의 `== null` 오버로드를 타지 않는다 — 파괴된 뒤에도
    /// 참조 자체는 non-null로 남는(fake-null) 대상을 걸러 stale `Prompt`/`Target`/`End`
    /// 호출을 막는다. 디스폰된 네트워크 오브젝트도 같이 걸러낸다.
    static bool IsAlive(IInteractable candidate) =>
        candidate is MonoBehaviour behaviour && behaviour != null &&
        (candidate is not NetworkBehaviour network || network.IsSpawned);

    public string Prompt
    {
        get
        {
            var target = cachedTarget;
            return IsAlive(target) ? target.PromptFor(Context) : string.Empty;
        }
    }

    /// 지금 상호작용 중인 대상. `BeginClient`와 `EndClient` 사이에만 있다. 어떤 박스를
    /// 잡고 있는지 아는 유일한 지점이라 루팅 창을 여닫는 쪽이 여기를 읽는다.
    public IInteractable Current => current;

    /// 지금 F가 닿는 대상. **프롬프트가 가리키는 것과 같은 것**이고, 테두리(`TargetOutline`)도
    /// 여기를 읽는다 — 안내와 테두리가 서로 다른 설비를 가리키면 둘 다 못 믿게 된다.
    public IInteractable Target => IsAlive(cachedTarget) ? cachedTarget : null;

    /// F가 아무 대상에도 닿지 않았을 때 프롬프트 자리에 「안 됨」을 띄우는 시간 (기획서 5.7.3).
    [SerializeField, Min(0f)] float deniedSeconds = 0.8f;
    float deniedUntil = float.NegativeInfinity;

    /// 방금 F가 먹지 않았다. 소리(`NotAllowed`)와 같은 순간에 켜진다.
    public bool Denied => Time.unscaledTime < deniedUntil;

    /// 마지막으로 F를 누른 대상. `Current`와 달리 F를 놓아도 남는다 — 재료 칸의 그리드
    /// 창은 누르고 있는 동안이 아니라 닫을 때까지 떠 있다 (기획서 6.5.4). 창을 여는 쪽이
    /// 거리를 다시 확인하므로, 여기 남아 있다는 것만으로 창이 뜨지는 않는다.
    public IInteractable Latest { get; private set; }

    void OnTriggerEnter(Collider other)
    {
        if (!IsOwner) return;
        // 남의 카페 설비는 넣지 않는다. 감지 구가 울타리를 넘어 닿아도 서버가 거절해 프롬프트만 헛돈다.
        var cafe = Cafe.Of(other);
        if (cafe != null && cafe.TeamId != PlayerTeam.Local()) return;
        foreach (var behaviour in other.GetComponentsInParent<MonoBehaviour>())
            if (behaviour is IInteractable candidate) AddCandidate(candidate);
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsOwner) return;
        foreach (var behaviour in other.GetComponentsInParent<MonoBehaviour>())
            if (behaviour is IInteractable candidate) RemoveCandidate(candidate);
    }

    void AddCandidate(IInteractable candidate)
    {
        candidateRefs.TryGetValue(candidate, out var count);
        candidateRefs[candidate] = count + 1;
        if (count > 0) return;

        candidates.Add(candidate);
        if (candidate is PlayerCarry carry) candidateTeams[candidate] = carry.GetComponent<PlayerTeam>();
    }

    void RemoveCandidate(IInteractable candidate)
    {
        if (!candidateRefs.TryGetValue(candidate, out var count)) return;
        if (count > 1) { candidateRefs[candidate] = count - 1; return; }

        candidateRefs.Remove(candidate);
        candidates.Remove(candidate);
        candidateTeams.Remove(candidate);
    }

    /// 버튼을 실제로 누른 순간이라 캐시를 쓰지 않고 다시 훑는다 — 지난 프레임의 대상이
    /// 그사이 손 상태나 거리 조건을 벗어났을 수 있다.
    public void BeginClient()
    {
        if (!IsOwner) return;
        if (CompletionGauge.TryStopLocalClient()) return;

        current = Nearest();
        if (current == null && phase != null && phase.Started && !phase.Finished && phase.Current != Phase.Transition)
        {
            SoundPlayed?.Invoke(SfxCue.NotAllowed, transform.position, -1);
            deniedUntil = Time.unscaledTime + deniedSeconds;
        }
        if (current != null) Latest = current;
        current?.BeginInteractionClient();
    }

    public void EndClient()
    {
        // 잡고 있던 대상이 홀드 도중 디스폰될 수 있다(가방을 다 파내면 서버가 바로 없앤다).
        // `current`는 인터페이스 참조라 Unity의 파괴 판정을 타지 않으므로 `IsAlive`로
        // 직접 걸러야 스폰되지 않은 NetworkBehaviour에 RPC를 보내지 않는다.
        if (IsAlive(current)) current.EndInteractionClient();
        current = null;
    }

    public void DumpClient()
    {
        if (!IsOwner) return;
        var sink = Nearest() as Sink;
        if (sink != null) sink.DiscardRpc();
        else GetComponent<PlayerInventory>()?.DumpRpc();
    }

    /// 낮에만, 그리고 손이 있을 때만 대상에게 묻는다. 설비별 규칙은 각 대상이
    /// `CanPromptClient`로 스스로 답한다 — 여기는 공통 게이트만 본다.
    bool CanPrompt(IInteractable target)
    {
        var director = MatchDirector.Instance;
        if (director == null || director.Phase.Current != Phase.Day || localCarry == null) return true;
        return target.CanPromptClient(Context);
    }

    // 기획서 5.7.4: 손 상태로 불가능한 프롬프트는 숨긴다. 실행 권한은 RPC가 재검증한다.
    public static bool CanUseFacility(FacilityKind kind, CarryView held, int dirtyStock = 0)
    {
        if (kind == FacilityKind.Sink && held.Empty) return dirtyStock > 0;
        if (held.HasDish && held.Dirty) return true; // 「세척 필요」는 예외 안내다.
        return SharedFacility.Accepts(kind, held);
    }

    IInteractable Nearest()
    {
        IInteractable best = null;
        var bestDistance = float.MaxValue;
        var localTeam = PlayerTeam.Local();
        for (var i = candidates.Count - 1; i >= 0; i--)
        {
            var candidate = candidates[i];
            if (!IsAlive(candidate))
            {
                candidates.RemoveAt(i);
                candidateRefs.Remove(candidate);
                candidateTeams.Remove(candidate);
                continue;
            }

            if (candidate is PlayerCarry carry)
            {
                if (carry.OwnerClientId == OwnerClientId) continue;
                candidateTeams.TryGetValue(candidate, out var team);
                if (team == null || team.Team != localTeam) continue;
            }
            if (!CanPrompt(candidate)) continue;

            var behaviour = (MonoBehaviour)candidate;
            var distance = Vector3.SqrMagnitude(transform.position - behaviour.transform.position);
            if (distance >= bestDistance) continue;
            best = candidate;
            bestDistance = distance;
        }
        return best;
    }

    // --- 상자 홀드 (기획서 6.5.1). 예전 Night/PlayerInteract. ---
    //
    // 박스 앞에서 F 홀드: 게이지를 채우면 루팅 창이 열리고, 그 뒤로는 창에서 칸을 눌러
    // 담는다. 이 구간은 홀드가 끝났는지 판단하지 않는다. "이 박스를 누르기 시작했다",
    // "뗐다", "이 칸을 눌렀다"만 보고하고 나머지는 서버(`ItemBox`)가 잰다. 소유자가 자기
    // 홀드를 재던 탓에 밤 파밍 루프가 공짜였다 (아키텍처_v1.0.md §1.1). 또한 어떤 박스를
    // 붙잡고 있는지 아는 유일한 지점이라, 대시 중단 처리(`DashHarass`)가 여기를 필요로 한다.

    /// 소유자 측: 서버에 알린 대상. F를 놓아도 루팅 세션이 살아 있으므로 여기서 놓지 않는다.
    /// 창을 여닫는 `MatchFlow`가 이 참조와 `ItemBox.Opened`만 보고 판단한다.
    ItemBox boxHeld;

    ItemBox boxServerHeld;     // 서버 측 진실. 대시 중단과 칸 담기가 여기로 간다

    /// 지금 루팅 창을 띄워야 할 박스. 세션이 닫히면 `ItemBox.Opened`가 false가 된다.
    public ItemBox LootBox => boxHeld;

    /// 개봉 게이지 진행도(0~1). 서버가 알려 준 캐스팅 상태를 읽는다 (`ItemBox.CastProgress01`).
    public float CastProgress01 => boxHeld != null && !boxHeld.Opened ? boxHeld.CastProgress01 : 0f;

    public void BeginBoxClient(ItemBox box)
    {
        if (!IsOwner || box == null) return;

        if (boxHeld != box)
        {
            if (boxHeld != null) CloseBoxRpc();
            boxHeld = box;
        }

        HoldBeginRpc(boxHeld.NetworkObject);
    }

    /// F를 놓았다. 캐스팅만 끝난다 — 이미 열린 창은 이동하거나 맞을 때까지 유지된다.
    public void EndBoxClient()
    {
        if (!IsOwner) return;
        if (boxHeld != null) HoldEndRpc();
    }

    /// 루팅 창에서 칸을 눌렀다. 담을 수 있는지는 서버가 다시 판단한다.
    public void TakeSlotClient(int index)
    {
        if (!IsOwner || boxHeld == null || !boxHeld.Opened) return;
        TakeSlotRpc(index);
    }

    /// 창을 스스로 닫는다(다른 박스로 옮기거나 UI를 닫을 때). 세션은 서버가 지운다.
    public void CloseBoxClient()
    {
        if (!IsOwner || boxHeld == null) return;
        CloseBoxRpc();
        boxHeld = null;
    }

    /// 발신자가 소유자인지 검사한다. NetworkBehaviour의 `[Rpc(SendTo.Server)]`는 어떤
    /// 클라이언트든 호출할 수 있고, 바로 이 검사가 빠져서 한 팀이 다른 팀의 완성 게이지를
    /// 멈출 수 있었다 (아키텍처_v1.0.md §1.2).
    [Rpc(SendTo.Server)]
    void HoldBeginRpc(NetworkObjectReference box, RpcParams p = default)
    {
        if (p.Receive.SenderClientId != OwnerClientId) return;
        if (!box.TryGet(out var no)) return;

        var target = no.GetComponent<ItemBox>();
        if (target == null) return;

        if (boxServerHeld != target) ReleaseBoxServer();
        boxServerHeld = target;
        target.BeginHoldServer(OwnerClientId);
    }

    [Rpc(SendTo.Server)]
    void HoldEndRpc(RpcParams p = default)
    {
        if (p.Receive.SenderClientId != OwnerClientId) return;
        if (boxServerHeld != null) boxServerHeld.EndHoldServer(OwnerClientId);
    }

    /// 담을 칸을 서버에 알린다. `HoldBeginRpc`와 같은 이유로 발신자가 소유자인지 검사한다 —
    /// 없으면 아무 클라이언트나 남의 상자를 대신 털 수 있다.
    [Rpc(SendTo.Server)]
    void TakeSlotRpc(int index, RpcParams p = default)
    {
        if (p.Receive.SenderClientId != OwnerClientId) return;
        if (boxServerHeld != null) boxServerHeld.TakeStackServer(OwnerClientId, index);
    }

    [Rpc(SendTo.Server)]
    void CloseBoxRpc(RpcParams p = default)
    {
        if (p.Receive.SenderClientId != OwnerClientId) return;
        ReleaseBoxServer();
    }

    void ReleaseBoxServer()
    {
        if (boxServerHeld != null) boxServerHeld.CancelSessionServer(OwnerClientId);
        boxServerHeld = null;
    }

    /// 대시를 맞았다. 개봉 캐스팅 중이었는지 이미 열린 세션이었는지는 `ItemBox`가
    /// 갈라 처리한다(기획서 6.6) — 여기서는 어느 상자를 잡고 있었는지만 넘긴다.
    public void InterruptServer()
    {
        if (!IsServer || boxServerHeld == null) return;
        boxServerHeld.HarassServer(OwnerClientId);
    }
}
