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
                case AvatarLocomotionOverrides.OverrideID.WALK_SPEED: locomotionOverrides.WalkSpeed = value; break;
                case AvatarLocomotionOverrides.OverrideID.JOG_SPEED: locomotionOverrides.JogSpeed = value; break;
                case AvatarLocomotionOverrides.OverrideID.RUN_SPEED: locomotionOverrides.RunSpeed = value; break;
                case AvatarLocomotionOverrides.OverrideID.JUMP_HEIGHT: locomotionOverrides.JumpHeight = value; break;
                case AvatarLocomotionOverrides.OverrideID.RUN_JUMP_HEIGHT: locomotionOverrides.RunJumpHeight = value; break;
                case AvatarLocomotionOverrides.OverrideID.HARD_LANDING_COOLDOWN: locomotionOverrides.HardLandingCooldown = value; break;
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
                AvatarLocomotionOverrides.OverrideID.WALK_SPEED => locomotionOverrides.WalkSpeed,
                AvatarLocomotionOverrides.OverrideID.JOG_SPEED => locomotionOverrides.JogSpeed,
                AvatarLocomotionOverrides.OverrideID.RUN_SPEED => locomotionOverrides.RunSpeed,
                AvatarLocomotionOverrides.OverrideID.JUMP_HEIGHT => locomotionOverrides.JumpHeight,
                AvatarLocomotionOverrides.OverrideID.RUN_JUMP_HEIGHT => locomotionOverrides.RunJumpHeight,
                AvatarLocomotionOverrides.OverrideID.HARD_LANDING_COOLDOWN => locomotionOverrides.HardLandingCooldown,
                _ => throw new ArgumentException(),
            };
    }
}
