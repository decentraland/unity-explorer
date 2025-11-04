using System;

namespace DCL.SDKComponents.AvatarLocomotion.Components
{
    public struct AvatarLocomotionOverrides
    {
        public static readonly AvatarLocomotionOverrides NO_OVERRIDES = new ();

        [Flags]
        public enum OverrideID : byte
        {
            WALK_SPEED = 1,
            JOG_SPEED = 1 << 1,
            RUN_SPEED = 1 << 2,
            JUMP_HEIGHT = 1 << 3,
            RUN_JUMP_HEIGHT = 1 << 4,
        }

        public OverrideID WriteMask;
        public float WalkSpeed;
        public float JogSpeed;
        public float RunSpeed;
        public float JumpHeight;
        public float RunJumpHeight;
    }
}
