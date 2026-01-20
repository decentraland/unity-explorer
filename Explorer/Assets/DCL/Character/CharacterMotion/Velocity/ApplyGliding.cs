using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion
{
    public static class ApplyGliding
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(in ICharacterControllerSettings settings, in CharacterRigidTransform rigidTransform, in JumpInputComponent jump, ref GlideState glideState, int physicsTick)
        {
            bool canGlide = rigidTransform.JumpCount > settings.AirJumpCount;
            bool wantsToGlide = jump.IsPressed && jump.Trigger.IsAvailable(physicsTick, 0);

            // Start gliding if can and want
            glideState.IsGliding |= canGlide && wantsToGlide;

            // Stop gliding if the jump button is released or we touched ground
            glideState.IsGliding &= jump.IsPressed && !rigidTransform.IsGrounded;

            if (glideState.IsGliding)
            {
                rigidTransform.GravityVelocity = Vector3.ClampMagnitude(rigidTransform.GravityVelocity, 1);
            }
        }
    }
}
