using UnityEngine;

namespace DCL.CharacterCamera.Components
{
    /// <summary>
    ///     Processed input suitable for camera controls.
    ///     Abstracted from the source it is originated from.
    ///     Having values on this component means that it will be actually applied and won't be filtered
    /// </summary>
    public struct CameraInput
    {
        public Vector2 Axes;

        public float WheelVerticalValue;
    }
}
