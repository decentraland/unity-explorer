using UnityEngine;

namespace ECS.CharacterMotion.Components
{
    public struct CharacterPhysics
    {
        public readonly CapsuleCollider Collider;

        /// <summary>
        ///     Whether the character is grounded
        /// </summary>
        public bool IsGrounded;

        public Vector3 Velocity;

        /// <summary>
        ///     TODO Collider if <see cref="IsGrounded" /> is true
        /// </summary>
        public Collider GroundedCollider;

        public CharacterPhysics(CapsuleCollider collider) : this()
        {
            Collider = collider;
        }
    }
}
