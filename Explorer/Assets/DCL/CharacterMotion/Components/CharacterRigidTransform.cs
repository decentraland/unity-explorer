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

        // General physics flags
        public bool IsGrounded;
        public bool IsOnASteepSlope;
        public float SteepSlopeTime;
        public float SteepSlopeAngle;
        public bool IsCollidingWithWall;

        // Buffers used to decide if the character can jump
        public int LastGroundedFrame;
        public int LastJumpFrame;
        public bool JustJumped;

        // Current velocity of the gravity
        public Vector3 GravityVelocity;

        // This is a modifier to the gravity direction, used by slope falling to ensure a smooth slide
        public Vector3 GravityDirection;

        // The result gravity when on a slope
        public Vector3 SlopeGravity;

        // Current Look direction of the character
        public Vector3 LookDirection = Vector3.forward;

        // Current Normal of the slope
        public Vector3 CurrentSlopeNormal;

        // The last calculated platform delta
        public Vector3 PlatformDelta;

        // This flag is set when the rigidTransform is between 2 slopes
        public bool IsStuck;

        public struct MovementVelocity
        {
            // Current sideways velocity
            public float XVelocity;

            // Current frontal velocity
            public float ZVelocity;

            // Sideways velocity dampening
            public float XDamp;

            // Frontal velocity dampening
            public float ZDamp;

            // Current acceleration weight (0 to 1) to decide which acceleration we have based on a curve
            public float AccelerationWeight;

            // Current Velocity
            public Vector3 Velocity;
        }

    }
}
