using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion
{
    public static class ApplyJump
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(
            ICharacterControllerSettings settings,
            ref CharacterRigidTransform characterPhysics,
            ref JumpInputComponent jump,
            in Vector3 viewerForward,
            in Vector3 viewerRight,
            in MovementInputComponent inputComponent,
            int physicsTick)
        {
            characterPhysics.JustJumped = false;

            // Cannot jump on steep slopes unless stuck
            bool isGrounded = characterPhysics.IsGrounded && (!characterPhysics.IsOnASteepSlope || characterPhysics.IsStuck);
            if (isGrounded)
            {
                characterPhysics.LastGroundedFrame = physicsTick;
                characterPhysics.JumpCount = 0;
            }

            // We calculate the bonus frames that we have after we decide to jump, settings.JumpGraceTime is in seconds, we convert it into physics ticks
            // The bonus frames are used for BOTH input buffering and coyote time
            int bonusFrames = Mathf.RoundToInt(settings.JumpGraceTime / UnityEngine.Time.fixedDeltaTime);

            // Reset the input buffering / coyote time windows if
            // - Positive Y velocity, so we already jumped, no more coyote time
            // - The simulation just started, and we are within the input buffering window, otherwise the character will jump on its own
            if (characterPhysics.GravityVelocity.y > 0 || physicsTick < bonusFrames) bonusFrames = 0;

            bool canJump = CanJump(settings, characterPhysics, physicsTick, bonusFrames);
            bool wantsToJump = jump.Trigger.IsAvailable(physicsTick, bonusFrames);

            if (canJump && wantsToJump)
            {
                TryApplyDirectionChangeImpulse(settings, characterPhysics, viewerForward, viewerRight, inputComponent);

                float jumpHeight = GetJumpHeight(characterPhysics.JumpCount, characterPhysics.MoveVelocity.Velocity, settings, inputComponent);
                float gravity = settings.Gravity * settings.JumpGravityFactor;

                // Override velocity in a jump direction
                characterPhysics.GravityVelocity.y = Mathf.Sqrt(-2 * jumpHeight * gravity);

                characterPhysics.IsGrounded = false;
                characterPhysics.LastJumpFrame = physicsTick;

                // We "consume" the jump input
                jump.Trigger.TickWhenJumpOccurred = int.MinValue;
                jump.Trigger.TickWhenJumpWasConsumed = physicsTick;
                characterPhysics.JustJumped = true;
                characterPhysics.JumpCount++;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool CanJump(ICharacterControllerSettings settings, in CharacterRigidTransform characterPhysics, int tick, int coyoteTimeTickCount)
        {
            bool isFirstJump = characterPhysics.JumpCount == 0;
            bool isGroundedOrCoyote = characterPhysics.IsGrounded || tick - characterPhysics.LastGroundedFrame < coyoteTimeTickCount;

            // Ensure the player is grounded if it's the 1st jump, otherwise just don't exceed max number of jumps
            int maxJumpCount = 1 + Mathf.Max(settings.AirJumpCount, 0);
            bool canJump = (isFirstJump && isGroundedOrCoyote) || characterPhysics.JumpCount < maxJumpCount;

            // Enforce the cooldown period between jumps
            if (!isFirstJump)
            {
                float timeSinceJumpStarted = (tick - characterPhysics.LastJumpFrame) * UnityEngine.Time.fixedDeltaTime;
                canJump &= timeSinceJumpStarted >= settings.CooldownBetweenJumps;
            }

            return canJump;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void TryApplyDirectionChangeImpulse(ICharacterControllerSettings settings, CharacterRigidTransform characterPhysics, in Vector3 viewerForward, in Vector3 viewerRight, in MovementInputComponent inputComponent)
        {
            // For the 1st jump we just keep the current velocity
            if (characterPhysics.JumpCount <= 0) return;

            // For later jumps we do apply an immediate change in direction
            float impulse = Mathf.Max(settings.AirJumpDirectionChangeImpulse, characterPhysics.MoveVelocity.Velocity.magnitude);
            var localVelocity = impulse * inputComponent.Axes;

            characterPhysics.MoveVelocity.XVelocity = localVelocity.x;
            characterPhysics.MoveVelocity.ZVelocity = localVelocity.y;
            characterPhysics.MoveVelocity.Velocity = (viewerForward * localVelocity.y) + (viewerRight * localVelocity.x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetJumpHeight(int jumpCount, Vector3 flatHorizontalVelocity, ICharacterControllerSettings settings, in MovementInputComponent input)
        {
            float jumpHeight;

            if (jumpCount == 0)
            {
                float maxJumpHeight = input.Kind switch
                                      {
                                          MovementKind.WALK => settings.JogJumpHeight,
                                          MovementKind.JOG => settings.JogJumpHeight,
                                          MovementKind.IDLE => settings.JogJumpHeight,
                                          MovementKind.RUN => settings.RunJumpHeight,
                                          _ => throw new ArgumentOutOfRangeException(),
                                      };

                float currentSpeed = flatHorizontalVelocity.magnitude;
                jumpHeight = Mathf.Lerp(settings.JogJumpHeight, maxJumpHeight, currentSpeed / settings.RunSpeed);
            }
            else
                jumpHeight = settings.AirJumpHeight;

            return jumpHeight;
        }
    }
}
