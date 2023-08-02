using UnityEngine;

namespace ECS.CharacterMotion.Components
{
    public struct CharacterPhysics
    {
        /// <summary>
        ///     Whether the character is grounded
        /// </summary>
        public bool IsGrounded;

        public Vector3 Velocity;

        /// <summary>
        ///     TODO Collider if <see cref="IsGrounded" /> is true
        /// </summary>
        public Collider GroundedCollider;
    }
}
