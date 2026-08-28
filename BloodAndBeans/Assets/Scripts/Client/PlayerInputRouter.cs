using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// 디바이스 입력을 소유한다. BB.Game은 의도만 전달받는다.
public class PlayerInputRouter : NetworkBehaviour
{
    [SerializeField] InputActionAsset actions;

    PlayerMove movement;
    PlayerInteractor interaction;
    PlayerInventory inventory;
    DashHarass dash;
    InputAction moveAction;
    InputAction interactAction;
    InputAction dashAction;
    InputAction dumpAction;
    InputAction buryAction;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner || actions == null) return;

        movement = GetComponent<PlayerMove>();
        interaction = GetComponent<PlayerInteractor>();
        inventory = GetComponent<PlayerInventory>();
        dash = GetComponent<DashHarass>();
        moveAction = actions.FindAction("Player/Move", true);
        interactAction = actions.FindAction("Player/Interact", true);
        dashAction = actions.FindAction("Player/Jump", true);
        dumpAction = actions.FindAction("Player/Crouch", true);

        // 가방 묻기는 액션 애셋에 이미 있고 쓰이지 않던 Sprint를 쓴다 (키보드 Left Shift,
        // 게임패드 좌스틱 누르기). 새 바인딩을 만들지 않았다.
        buryAction = actions.FindAction("Player/Sprint", true);

        moveAction.performed += OnMove;
        moveAction.canceled += OnMove;
        interactAction.started += OnInteractStarted;
        interactAction.canceled += OnInteractCanceled;
        dashAction.performed += OnDash;
        dumpAction.performed += OnDump;
        buryAction.performed += OnBury;
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
    }

    void OnMove(InputAction.CallbackContext context) =>
        movement?.SetInputClient(context.ReadValue<Vector2>());

    void OnInteractStarted(InputAction.CallbackContext _) => interaction?.BeginClient();
    void OnInteractCanceled(InputAction.CallbackContext _) => interaction?.EndClient();
    void OnDash(InputAction.CallbackContext _) => dash?.DashRpc();
    void OnDump(InputAction.CallbackContext _) => interaction?.DumpClient();
    void OnBury(InputAction.CallbackContext _) => inventory?.BuryRpc();
}
