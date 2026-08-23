using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

/// Owner-authoritative movement. Pairs with NetworkTransform in Owner authority mode.
/// Reads the keyboard directly — this project uses the Input System package.
public class PlayerMove : NetworkBehaviour
{
    [SerializeField] float speed = 5f;

    void Update()
    {
        if (!IsOwner) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        var move = Vector3.zero;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.z -= 1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.z += 1f;

        if (move.sqrMagnitude > 1f) move.Normalize();

        transform.position += move * (speed * Time.deltaTime);
    }
}
