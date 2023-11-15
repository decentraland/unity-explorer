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
        public MovementVelocity MoveVelocity;

        /// <summary>
        ///     Whether the character is grounded
        /// </summary>
        public bool IsGrounded;

        public int LastGroundedFrame;

        public int LastJumpFrame;

        /// <summary>
        ///     Every velocity that is applied as is
        /// </summary>
        public Vector3 NonInterpolatedVelocity;

        public Vector3 LookDirection = Vector3.forward;

        public struct MovementVelocity
        {
            /// <summary>
            ///     Current sideways velocity
            /// </summary>
            public float XVelocity;

            /// <summary>
            ///     Current frontal velocity
            /// </summary>
            public float ZVelocity;

            /// <summary>
            ///     Sideways velocity dampening
            /// </summary>
            public float XDamp;

            /// <summary>
            ///     Frontal velocity dampening
            /// </summary>
            public float ZDamp;

            /// <summary>
            ///     Current acceleration weight (0 to 1) to decide which acceleration we have based on a curve
            /// </summary>
            public float AccelerationWeight;

            /// <summary>
            ///     Set by physics system in FixedUpdate
            /// </summary>
            public Vector3 Velocity;
        }
    }
}
