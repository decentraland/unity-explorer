using UnityEngine;
using UnityEngine.InputSystem;

namespace SceneRuntime.WebClient.Bootstrapper
{
    /// <summary>
    /// Simple fly camera controller using the new Unity Input System.
    /// Controls:
    /// - WASD: Horizontal movement
    /// - Mouse: Look/rotation
    /// - Q/E: Up/down movement
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class SimpleCameraNewInput : MonoBehaviour
    {
        [Header("Movement")]
        [SerializeField] private float moveSpeed = 10f;
        [SerializeField] private float verticalSpeed = 10f;

        [Header("Look")]
        [SerializeField] private float lookSensitivity = 0.1f;
        [SerializeField] private float maxPitch = 89f;

        // Input actions
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction verticalAction;

        // State
        private float pitch;
        private float yaw;

        private void Awake()
        {
            // Initialize rotation from current transform
            Vector3 euler = transform.eulerAngles;
            yaw = euler.y;
            pitch = euler.x;
            if (pitch > 180f) pitch -= 360f; // Normalize pitch to -180 to 180

            // Create input actions
            CreateInputActions();
        }

        private void CreateInputActions()
        {
            // Movement - WASD composite (Vector2)
            moveAction = new InputAction("Move", InputActionType.Value);
            moveAction.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");

            // Look - Mouse Delta (Vector2)
            lookAction = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");

            // Vertical movement - Q/E (float: -1 to 1)
            verticalAction = new InputAction("Vertical", InputActionType.Value);
            verticalAction.AddCompositeBinding("1DAxis")
                .With("Negative", "<Keyboard>/q")
                .With("Positive", "<Keyboard>/e");
        }

        private void OnEnable()
        {
            moveAction?.Enable();
            lookAction?.Enable();
            verticalAction?.Enable();

            // Lock cursor for mouse look
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            moveAction?.Disable();
            lookAction?.Disable();
            verticalAction?.Disable();

            // Restore cursor
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnDestroy()
        {
            moveAction?.Dispose();
            lookAction?.Dispose();
            verticalAction?.Dispose();
        }

        private void Update()
        {
            HandleLook();
            HandleMovement();
        }

        private void HandleLook()
        {
            Vector2 lookDelta = lookAction.ReadValue<Vector2>();

            yaw += lookDelta.x * lookSensitivity;
            pitch -= lookDelta.y * lookSensitivity;
            pitch = Mathf.Clamp(pitch, -maxPitch, maxPitch);

            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }

        private void HandleMovement()
        {
            Vector2 moveInput = moveAction.ReadValue<Vector2>();
            float verticalInput = verticalAction.ReadValue<float>();

            // Calculate movement direction relative to camera orientation
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            // Keep horizontal movement on the XZ plane
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 movement = Vector3.zero;
            movement += forward * moveInput.y; // W/S
            movement += right * moveInput.x;   // A/D
            movement += Vector3.up * verticalInput; // Q/E

            transform.position += movement * moveSpeed * Time.deltaTime;
        }
    }
}
