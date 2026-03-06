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
            in JumpInputComponent jumpInput,
            ref GlideState glideState,
            int physicsTick,
            float dt)
        {
            // When this flag is true we can press the button and keep holding it
            // It's a form of input buffering that makes gameplay smoother
            bool canTriggerGliding = jumpState.JumpCount > jumpState.MaxAirJumpCount &&
                                     !rigidTransform.IsGrounded &&
                                     rigidTransform.GroundDistance > settings.GlideMinGroundDistance;

            // Player can hold the glide button to start gliding as soon as possible
            // (hence the OR, because the button input will be in the available state only for 1 tick)
            glideState.WantsToGlide |= canTriggerGliding && jumpInput.Trigger.IsAvailable(physicsTick, 0);

            // Obviously we still want to reset the flag if we release the button
            glideState.WantsToGlide &= jumpInput.IsPressed;

            // Now that we decided whether or not the player wants to start gliding (and can do so) we need to consider animations and cooldowns
            // Once the 'ready' flag becomes true the actual gliding sequence starts
            bool enoughTimeSinceLastJump = (physicsTick - jumpInput.Trigger.TickWhenJumpWasConsumed) * dt >= settings.JumpToGlideTimeInterval;
            bool coolingDown = (physicsTick - glideState.CooldownStartedTick) * dt < settings.GlideCooldown;
            bool readyToStartGliding = enoughTimeSinceLastJump && !coolingDown && glideState.Value == GlideStateValue.PROP_CLOSED;

            // Start gliding if want, can and ready
            if (glideState.WantsToGlide && canTriggerGliding && readyToStartGliding)
            {
                glideState.Value = GlideStateValue.OPENING_PROP;
                return;
            }

            if (glideState.Value == GlideStateValue.GLIDING)
            {
                if (!jumpInput.IsPressed || !canTriggerGliding)
                    // Stop gliding if the jump button is released or any other condition prevents it
                    glideState.Value = GlideStateValue.CLOSING_PROP;
                else
                    // Otherwise clamp the gravity
                    rigidTransform.GravityVelocity = Vector3.ClampMagnitude(rigidTransform.GravityVelocity, settings.GlideMaxGravity);
            }
        }
    }
}
