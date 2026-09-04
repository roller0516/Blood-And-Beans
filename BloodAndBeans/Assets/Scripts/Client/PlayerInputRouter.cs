using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// 디바이스 입력을 소유한다. BB.Game은 의도만 전달받는다.
///
/// **이동 입력은 여기서 카메라 기준으로 돌려 월드 방향으로 만든다.** 카메라가 플레이어를
/// 중심으로 도는 3인칭이 되면서(폴 가이즈식) 스틱의 위쪽이 화면의 위쪽을 뜻하게 됐다.
/// 회전을 여기서 끝내는 이유는 권위 때문이다 — `PlayerMove`는 받은 벡터를 그대로 월드
/// 방향으로 쓰고 서버와 소유자가 같은 식을 돌린다. 카메라를 아는 것은 클라이언트뿐이므로
/// 서버가 카메라를 몰라도 되도록 이미 돌아간 값을 보낸다.
public class PlayerInputRouter : NetworkBehaviour
{
    [SerializeField] InputActionAsset actions;

    /// 카메라가 도는 동안에도 방향을 다시 보내기 위한 최소 변화량. 매 프레임 보내면
    /// 스틱을 쥐고만 있어도 RPC가 프레임 수만큼 나간다.
    [SerializeField] float resendThreshold = 0.02f;

    /// 이동 입력을 돌릴 기준. 비면 `Camera.main`을 한 번 찾아 캐시한다.
    Transform cameraBasis;

    Vector2 sentInput;

    /// 지난 프레임의 차단 상태. 전환을 봐야 멈춤을 한 번만 보낸다 — 매 프레임 (0,0)을
    /// 보내면 창을 열어 둔 내내 이동 RPC가 프레임 수만큼 나간다.
    bool inputBlocked;

    PlayerMove movement;
    PlayerInteractor interaction;
    PlayerInventory inventory;
    DashHarass dash;
    PlayerCharacter character;
    InputAction moveAction;
    InputAction interactAction;
    InputAction dashAction;
    InputAction dumpAction;
    InputAction buryAction;
    InputAction skillAction;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner || actions == null) return;

        movement = GetComponent<PlayerMove>();
        interaction = GetComponent<PlayerInteractor>();
        inventory = GetComponent<PlayerInventory>();
        dash = GetComponent<DashHarass>();
        character = GetComponent<PlayerCharacter>();
        moveAction = actions.FindAction("Player/Move", true);
        interactAction = actions.FindAction("Player/Interact", true);
        dashAction = actions.FindAction("Player/Jump", true);
        dumpAction = actions.FindAction("Player/Crouch", true);

        // 가방 묻기는 액션 애셋에 이미 있고 쓰이지 않던 Sprint를 쓴다 (키보드 Left Shift,
        // 게임패드 좌스틱 누르기). 새 바인딩을 만들지 않았다.
        buryAction = actions.FindAction("Player/Sprint", true);

        // 밤 액티브 스킬 (기획서 9.2). 액션 애셋에 이미 있고 쓰이지 않던 Previous를 쓴다
        // (키보드 1, 게임패드 D-pad 왼쪽). 새 바인딩을 만들지 않았다 — 묻기가 Sprint를
        // 쓴 것과 같은 방식이다.
        //
        // 기획서 11장은 "캐릭터 능력은 전부 패시브이므로 스킬 키가 없다"고 적혀 있지만,
        // 9.2가 밤 액티브를 정의하면서 그 문장은 낡았다. 차이를 보고만 하고 임의로 한쪽을
        // 바꾸지 않는다 (AGENTS.md) — 키는 9.2를 따라야 스킬이 존재할 수 있다.
        skillAction = actions.FindAction("Player/Previous", true);

        moveAction.performed += OnMove;
        moveAction.canceled += OnMove;
        interactAction.started += OnInteractStarted;
        interactAction.canceled += OnInteractCanceled;
        dashAction.performed += OnDash;
        dumpAction.performed += OnDump;
        buryAction.performed += OnBury;
        skillAction.performed += OnSkill;
        actions.FindActionMap("Player", true).Enable();
    }

    public override void OnNetworkDespawn()
    {
        if (moveAction == null) return;
        moveAction.performed -= OnMove;
        moveAction.canceled -= OnMove;
        interactAction.started -= OnInteractStarted;
        interactAction.canceled -= OnInteractCanceled;
        dashAction.performed -= OnDash;
        dumpAction.performed -= OnDump;
        buryAction.performed -= OnBury;
        if (skillAction != null) skillAction.performed -= OnSkill;
    }

    /// 밤 액티브 스킬 (기획서 9.2). 쿨다운과 페이즈 검사는 전부 서버가 한다 —
    /// 여기서는 눌렸다는 사실만 넘긴다.
    void OnSkill(InputAction.CallbackContext _)
    {
        if (Blocked || character == null) return;
        character.UseSkillRpc();
    }

    /// 조작을 막는 UI가 떠 있는가 (`UIManager.PlayerInputBlocked`). 설정 팝업이 그렇고,
    /// 상자 루팅 창은 아니다 — 기획서 6.5.5의 이동 취소가 이동 입력으로 발동한다.
    ///
    /// 여기서 한 번에 막는다. 액션마다 따로 판단하면 새 액션을 붙일 때 이 검사를 빠뜨린
    /// 것이 조용한 구멍으로 남는다.
    static bool Blocked => UIManager.Instance != null && UIManager.Instance.PlayerInputBlocked;

    void OnMove(InputAction.CallbackContext context)
    {
        if (Blocked) return;
        Send(context.ReadValue<Vector2>());
    }

    /// 스틱을 쥔 채 카메라만 돌 때도 방향이 따라와야 한다. 입력 콜백은 값이 바뀔 때만
    /// 오므로 그것만으로는 카메라 회전이 반영되지 않는다.
    void Update()
    {
        if (!IsOwner || moveAction == null) return;

        // 창이 열리는 순간 멈춘다. 누르고 있던 키의 입력 콜백은 이미 지나갔으므로
        // 여기서 끊지 않으면 창을 여는 동안 캐릭터가 그대로 걸어간다.
        var blocked = Blocked;
        if (blocked != inputBlocked)
        {
            inputBlocked = blocked;
            if (blocked) Send(Vector2.zero);
        }
        if (blocked) return;

        var raw = moveAction.ReadValue<Vector2>();
        if (raw.sqrMagnitude <= 0.0001f) return;
        Send(raw);
    }

    /// 화면 기준 입력을 월드 방향으로 돌려 보낸다.
    void Send(Vector2 raw)
    {
        if (movement == null) return;

        var world = ToWorld(raw);

        // 같은 값을 다시 보내지 않는다. 놓는 순간(0,0)은 반드시 보낸다 — 멈춤이 늦으면
        // 캐릭터가 계속 미끄러진다.
        if (world.sqrMagnitude > 0.0001f && (world - sentInput).sqrMagnitude < resendThreshold * resendThreshold)
            return;

        sentInput = world;
        movement.SetInputClient(world);
    }

    Vector2 ToWorld(Vector2 raw)
    {
        if (raw.sqrMagnitude <= 0.0001f) return Vector2.zero;

        // 늦게 생기는 참조라 없을 때만 한 번 찾는다 (AGENTS.md 참조와 결합도).
        if (cameraBasis == null)
        {
            var main = Camera.main;
            if (main == null) return raw;          // 카메라가 아직 없으면 월드 기준 그대로
            cameraBasis = main.transform;
        }

        // 카메라의 수평 방향만 쓴다. 내려다보는 각이 섞이면 앞으로 가는 양이 각도에 따라 준다.
        var forward = cameraBasis.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) return raw;
        forward.Normalize();

        var right = new Vector3(forward.z, 0f, -forward.x);
        var world = right * raw.x + forward * raw.y;
        return new Vector2(world.x, world.z);
    }

    void OnInteractStarted(InputAction.CallbackContext _)
    {
        if (Blocked) return;
        interaction?.BeginClient();
    }

    /// 뗀 것은 막지 않는다. 누른 채로 창이 열렸다면 그 홀드는 이미 시작돼 있고, 여기서
    /// 끊지 않으면 창을 닫을 때까지 F를 누르고 있는 상태로 남는다.
    void OnInteractCanceled(InputAction.CallbackContext _) => interaction?.EndClient();

    void OnDash(InputAction.CallbackContext _)
    {
        if (Blocked) return;
        dash?.DashRpc();
    }

    void OnDump(InputAction.CallbackContext _)
    {
        if (Blocked) return;
        interaction?.DumpClient();
    }

    void OnBury(InputAction.CallbackContext _)
    {
        if (Blocked) return;
        inventory?.BuryRpc();
    }
}
