using DCL.SDKComponents.AvatarLocomotion.Components;
using System;

namespace DCL.SDKComponents.AvatarLocomotion.Systems
{
    public static class AvatarLocomotionOverridesHelper
    {
        public static void SetValue(ref AvatarLocomotionOverrides locomotionOverrides, AvatarLocomotionOverrides.OverrideID id, float value)
        {
            switch (id)
            {
                case AvatarLocomotionOverrides.OverrideID.WalkSpeed: locomotionOverrides.WalkSpeed = value; break;
                case AvatarLocomotionOverrides.OverrideID.JogSpeed: locomotionOverrides.JogSpeed = value; break;
                case AvatarLocomotionOverrides.OverrideID.RunSpeed: locomotionOverrides.RunSpeed = value; break;
                case AvatarLocomotionOverrides.OverrideID.JumpHeight: locomotionOverrides.JumpHeight = value; break;
                case AvatarLocomotionOverrides.OverrideID.RunJumpHeight: locomotionOverrides.RunJumpHeight = value; break;
                case AvatarLocomotionOverrides.OverrideID.HardLandingCooldown: locomotionOverrides.HardLandingCooldown = value; break;
                case AvatarLocomotionOverrides.OverrideID.DoubleJumpHeight: locomotionOverrides.DoubleJumpHeight = value; break;
                case AvatarLocomotionOverrides.OverrideID.GlideSpeed: locomotionOverrides.GlidingSpeed = value; break;
                case AvatarLocomotionOverrides.OverrideID.GlideMaxGravity: locomotionOverrides.GlidingMaxGravity = value; break;
                default: throw new ArgumentException();
            }

            locomotionOverrides.WriteMask |= id;
        }

        public static void ClearValue(ref AvatarLocomotionOverrides locomotionOverrides, AvatarLocomotionOverrides.OverrideID id) =>
            locomotionOverrides.WriteMask &= ~id;

        public static void ClearAll(ref AvatarLocomotionOverrides locomotionOverrides) =>
            locomotionOverrides.WriteMask = 0;

        public static bool TryOverride(in AvatarLocomotionOverrides locomotionOverrides, AvatarLocomotionOverrides.OverrideID id, ref float value)
        {
            if ((locomotionOverrides.WriteMask & id) != 0)
            {
                value = GetValue(locomotionOverrides, id);
                return true;
            }
            return false;
        }

        private static float GetValue(in AvatarLocomotionOverrides locomotionOverrides, AvatarLocomotionOverrides.OverrideID id) =>
            id switch
            {
                AvatarLocomotionOverrides.OverrideID.WalkSpeed => locomotionOverrides.WalkSpeed,
                AvatarLocomotionOverrides.OverrideID.JogSpeed => locomotionOverrides.JogSpeed,
                AvatarLocomotionOverrides.OverrideID.RunSpeed => locomotionOverrides.RunSpeed,
                AvatarLocomotionOverrides.OverrideID.JumpHeight => locomotionOverrides.JumpHeight,
                AvatarLocomotionOverrides.OverrideID.RunJumpHeight => locomotionOverrides.RunJumpHeight,
                AvatarLocomotionOverrides.OverrideID.HardLandingCooldown => locomotionOverrides.HardLandingCooldown,
                AvatarLocomotionOverrides.OverrideID.DoubleJumpHeight => locomotionOverrides.DoubleJumpHeight,
                AvatarLocomotionOverrides.OverrideID.GlideSpeed => locomotionOverrides.GlidingSpeed,
                AvatarLocomotionOverrides.OverrideID.GlideMaxGravity => locomotionOverrides.GlidingMaxGravity,
                _ => throw new ArgumentException(),
            };
    }
}
