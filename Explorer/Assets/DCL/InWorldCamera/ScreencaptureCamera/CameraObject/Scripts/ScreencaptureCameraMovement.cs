using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.InWorldCamera.ScreencaptureCamera.CameraObject.Scripts
{
    public class ScreencaptureCameraMovement: MonoBehaviour
    {
        [SerializeField] private InputActionReference translationInputAction;  // Drag your Input Action here

        private Vector2 moveInput;

        private void OnTranslation(InputAction.CallbackContext context)
        {
            moveInput = translationInputAction.action.ReadValue<Vector2>();
        }
    }
}
