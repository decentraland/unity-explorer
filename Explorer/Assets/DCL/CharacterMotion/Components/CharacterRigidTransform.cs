using UnityEngine;

namespace DCL.CharacterMotion.Components
{
    /// <summary>
    ///     Characters do not contain rigid bodies but are driven by <see cref="CharacterController" />.
    ///     But all calculations of velocity happen in FixedUpdate.
    ///     <para>
    ///         Rotation is not driven by Rigid Transform but evaluated directly in Update as it does not impact Physics interactions
    ///     </para>
    ///     <para>
    ///         This component should be reused for other avatars to enable interpolation
    ///     </para>
    /// </summary>
    public class CharacterRigidTransform
    {
        public struct MovementVelocity
        {
            /// <summary>
            /// Set by physics system in FixedUpdate
            /// </summary>
            public Vector3 Target;

            /// <summary>
            /// Interpolated according to acceleration every Update
            /// </summary>
            public Vector3 Interpolated;
        }

        public MovementVelocity MoveVelocity;

        /// <summary>
        ///     Whether the character is grounded
        /// </summary>
        public bool IsGrounded;

        /// <summary>
        /// Every velocity that is applied as is
        /// </summary>
        public Vector3 NonInterpolatedVelocity;
    }
}
