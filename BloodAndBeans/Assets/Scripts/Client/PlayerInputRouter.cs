using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// Owns device input. BB.Game receives only intent.
public class PlayerInputRouter : NetworkBehaviour
{
    [SerializeField] InputActionAsset actions;

    PlayerMove movement;
    PlayerInteractor interaction;
    DashHarass dash;
    InputAction moveAction;
    InputAction interactAction;
    InputAction dashAction;
    InputAction dumpAction;

    public override void OnNetworkSpawn()
    {
        if (!IsOwner || actions == null) return;

        movement = GetComponent<PlayerMove>();
        interaction = GetComponent<PlayerInteractor>();
        dash = GetComponent<DashHarass>();
        moveAction = actions.FindAction("Player/Move", true);
        interactAction = actions.FindAction("Player/Interact", true);
        dashAction = actions.FindAction("Player/Jump", true);
        dumpAction = actions.FindAction("Player/Crouch", true);

        moveAction.performed += OnMove;
        moveAction.canceled += OnMove;
        interactAction.started += OnInteractStarted;
        interactAction.canceled += OnInteractCanceled;
        dashAction.performed += OnDash;
        dumpAction.performed += OnDump;
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
    }

    void OnMove(InputAction.CallbackContext context) =>
        movement?.SetInputClient(context.ReadValue<Vector2>());

    void OnInteractStarted(InputAction.CallbackContext _) => interaction?.BeginClient();
    void OnInteractCanceled(InputAction.CallbackContext _) => interaction?.EndClient();
    void OnDash(InputAction.CallbackContext _) => dash?.DashRpc();
    void OnDump(InputAction.CallbackContext _) => interaction?.DumpClient();
}
