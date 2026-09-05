using System;

namespace DCL.SDKComponents.AvatarLocomotion.Components
{
    public struct AvatarLocomotionOverrides
    {
        public static readonly AvatarLocomotionOverrides NO_OVERRIDES = new ();

        [Flags]
        public enum OverrideID
        {
            WalkSpeed = 1,
            JogSpeed = 1 << 1,
            RunSpeed = 1 << 2,
            JumpHeight = 1 << 3,
            RunJumpHeight = 1 << 4,
            HardLandingCooldown = 1 << 5,
            DoubleJumpHeight = 1 << 6,
            GlideSpeed = 1 << 7,
            GlideMaxGravity = 1 << 8,
        }

        public OverrideID WriteMask;
        public float WalkSpeed;
        public float JogSpeed;
        public float RunSpeed;
        public float JumpHeight;
        public float RunJumpHeight;
        public float HardLandingCooldown;
        public float DoubleJumpHeight;
        public float GlidingSpeed;
        public float GlidingMaxGravity;
    }
}
