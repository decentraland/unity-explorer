using ECS.Input.Component;
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
        /// Camera's point of view input
        /// </summary>
        public Vector2 POV;

        /// <summary>
        /// When in free camera mode, this is the movement vector
        /// </summary>
        public Vector2 FreeMovement;
    }
}
