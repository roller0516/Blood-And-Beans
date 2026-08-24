using Unity.Netcode;
using UnityEngine;

/// The owner sends bounded intent; the server owns the transform.
public class PlayerMove : NetworkBehaviour
{
    [SerializeField] float speed = 5f;

    PlayerInventory inventory;
    Vector2 moveInput;

    void Awake() => inventory = GetComponent<PlayerInventory>();

    void Update()
    {
        if (!IsServer || GetComponent<DashHarass>()?.IsStunnedServer == true) return;

        var load = inventory != null ? inventory.CurrentSpeedMultiplier : 1f;
        transform.position += new Vector3(moveInput.x, 0f, moveInput.y) * (speed * load * Time.deltaTime);
    }

    public void SetInputClient(Vector2 input)
    {
        if (IsOwner) SetInputRpc(Vector2.ClampMagnitude(input, 1f));
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Owner)]
    void SetInputRpc(Vector2 input) => moveInput = Vector2.ClampMagnitude(input, 1f);
}
