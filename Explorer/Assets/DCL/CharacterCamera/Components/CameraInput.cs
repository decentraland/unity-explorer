using DCL.Input.Component;
using UnityEngine;

namespace DCL.CharacterCamera.Components
{
    /// <summary>
    ///     Processed input suitable for camera controls.
    ///     Abstracted from the source it is originated from.
    ///     Having values on this component means that it will be actually applied and won't be filtered
    /// </summary>
    public struct CameraInput : IInputComponent
    {
        public bool ZoomIn;
        public bool ZoomOut;

        /// <summary>
        /// Camera's movement based on input
        /// </summary>
        public Vector2 Delta;

        /// <summary>
        /// When in free camera mode, this is the movement vector
        /// </summary>
        public Vector2 FreeMovement;

        /// <summary>
        /// Cursor lock state, when true the cursor is hidden and the camera can be moved
        /// </summary>
        public bool IsCursorLocked;
    }
}
