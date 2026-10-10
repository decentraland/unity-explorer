using UnityEngine;
using UnityEngine.InputSystem;

namespace DCL.Input.Utils
{
    public static class DCLInputUtilities
    {
        public static Vector2 GetPointerPosition(InputAction.CallbackContext context)
        {
            if (context.control is Pointer pCtrl) return pCtrl.position.ReadValue();
            if (Pointer.current != null) return Pointer.current.position.ReadValue();
            if (Mouse.current != null) return Mouse.current.position.ReadValue();
            if (Touchscreen.current?.primaryTouch != null) return Touchscreen.current.primaryTouch.position.ReadValue();
            return Vector2.zero;
        }
    }
}
