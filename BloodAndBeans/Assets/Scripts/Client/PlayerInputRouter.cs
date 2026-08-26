using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// 디바이스 입력을 소유한다. BB.Game은 의도만 전달받는다.
public class PlayerInputRouter : NetworkBehaviour
{
    [SerializeField] InputActionAsset actions;

    PlayerMove movement;
    PlayerInteractor interaction;
    PlayerInteract boxHold;
    DashHarass dash;
    InputAction moveAction;
    InputAction interactAction;
    InputAction dashAction;
    InputAction dumpAction;
    InputAction previousSlotAction;
    InputAction nextSlotAction;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner || actions == null) return;

        movement = GetComponent<PlayerMove>();
        interaction = GetComponent<PlayerInteractor>();
        boxHold = GetComponent<PlayerInteract>();
        dash = GetComponent<DashHarass>();
        moveAction = actions.FindAction("Player/Move", true);
        interactAction = actions.FindAction("Player/Interact", true);
        dashAction = actions.FindAction("Player/Jump", true);
        dumpAction = actions.FindAction("Player/Crouch", true);

        // 박스 칸 고르기는 액션 애셋에 이미 있고 쓰이지 않던 Previous/Next를 쓴다
        // (키보드 1/2, 게임패드 D-Pad 좌/우). 새 바인딩을 만들지 않았다.
        previousSlotAction = actions.FindAction("Player/Previous", true);
        nextSlotAction = actions.FindAction("Player/Next", true);

        moveAction.performed += OnMove;
        moveAction.canceled += OnMove;
        interactAction.started += OnInteractStarted;
        interactAction.canceled += OnInteractCanceled;
        dashAction.performed += OnDash;
        dumpAction.performed += OnDump;
        previousSlotAction.performed += OnPreviousSlot;
        nextSlotAction.performed += OnNextSlot;
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
        previousSlotAction.performed -= OnPreviousSlot;
        nextSlotAction.performed -= OnNextSlot;
    }

    void OnMove(InputAction.CallbackContext context) =>
        movement?.SetInputClient(context.ReadValue<Vector2>());

    void OnInteractStarted(InputAction.CallbackContext _) => interaction?.BeginClient();
    void OnInteractCanceled(InputAction.CallbackContext _) => interaction?.EndClient();
    void OnDash(InputAction.CallbackContext _) => dash?.DashRpc();
    void OnDump(InputAction.CallbackContext _) => interaction?.DumpClient();
    void OnPreviousSlot(InputAction.CallbackContext _) => boxHold?.MoveSelectionClient(-1);
    void OnNextSlot(InputAction.CallbackContext _) => boxHold?.MoveSelectionClient(1);
}
