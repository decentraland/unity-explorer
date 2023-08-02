using ECS.Input.Component;
using UnityEngine;

namespace ECS.CharacterMotion.Components
{
    public struct MovementInputComponent : IInputComponent
    {
        public MovementKind Kind;

        /// <summary>
        ///     The normalized value of axes (X, Y) that indicates pressing force
        ///     (0,0) means no movement
        /// </summary>
        public Vector2 Axes;
    }
}
