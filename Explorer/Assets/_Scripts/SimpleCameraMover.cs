using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Camera))]
public class SimpleCameraMover : MonoBehaviour
{
    [Tooltip("Units per second")]
    [SerializeField] private float moveSpeed = 5f;

    private void Update()
    {
        Keyboard kb = Keyboard.current;
        if (kb == null) return; // no keyboard connected?

        Vector3 move = Vector3.zero;

        // Horizontal plane
        if (kb.wKey.isPressed) move += transform.forward;
        if (kb.sKey.isPressed) move -= transform.forward;
        if (kb.aKey.isPressed) move -= transform.right;
        if (kb.dKey.isPressed) move += transform.right;

        // Vertical axis
        if (kb.eKey.isPressed) move += transform.up; // E = up
        if (kb.qKey.isPressed) move -= transform.up; // Q = down

        // Apply movement
        transform.position += move * (moveSpeed * Time.deltaTime);

        // One-shot example
        if (kb.spaceKey.wasPressedThisFrame) { Debug.Log("Space was pressed this frame!"); }
    }
}
