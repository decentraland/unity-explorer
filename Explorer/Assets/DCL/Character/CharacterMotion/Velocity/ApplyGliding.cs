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
        public static void Execute(in ICharacterControllerSettings settings,
            in CharacterRigidTransform rigidTransform,
            in JumpState jumpState,
            in JumpInputComponent jump,
            ref GlideState glideState,
            int physicsTick,
            float dt)
        {
            bool coolingDown = (physicsTick - glideState.CooldownStartedTick) * dt < settings.GlideCooldown;
            bool canGlide = jumpState.JumpCount > jumpState.MaxAirJumpCount &&
                            !rigidTransform.IsGrounded &&
                            rigidTransform.GroundDistance > settings.GlideMinGroundDistance &&
                            !coolingDown;

            // Allow pressing the glide button before the action is actually available
            // Player can hold the glide button to start gliding as soon as possible
            // We added this to handle the specific case of trying to open the glider while it's still being closed
            glideState.WantsToGlide |= canGlide && jump.Trigger.IsAvailable(physicsTick, 0);

            // Obviously we still want to reset the flag if we release the button
            glideState.WantsToGlide &= jump.IsPressed;

            // Start gliding if can and want
            if (glideState.Value == GlideStateValue.PROP_CLOSED && canGlide && glideState.WantsToGlide)
            {
                glideState.Value = GlideStateValue.OPENING_PROP;
                return;
            }

            if (glideState.Value == GlideStateValue.GLIDING)
            {
                if (!jump.IsPressed || !canGlide)
                    // Stop gliding if the jump button is released or any other condition prevents it
                    glideState.Value = GlideStateValue.CLOSING_PROP;
                else
                    // Otherwise clamp the gravity
                    rigidTransform.GravityVelocity = Vector3.ClampMagnitude(rigidTransform.GravityVelocity, settings.GlideMaxGravity);
            }
        }
    }
}
