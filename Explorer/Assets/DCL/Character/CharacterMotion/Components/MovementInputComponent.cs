using DCL.Input.Component;
using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    public struct MovementInputComponent : IInputComponent
    {
        public MovementKind Kind;

        /// <summary>
        ///     The normalized value of axes (X, Y) that indicates pressing force
        ///     (0,0) means no movement
        /// </summary>
        public Vector2 Axes;

        /// <summary>
        /// When enabled, the orientation of the camera will not affect the direction towards the avatar moves.
        /// </summary>
        public bool IgnoreCamera;

        /// <summary>
        /// Enabled during one frame when the player has pressed any input.
        /// </summary>
        public bool HasPlayerPressed;
    }
}
