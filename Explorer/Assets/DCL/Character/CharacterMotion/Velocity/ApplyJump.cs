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
        public static void Execute(ICharacterControllerSettings settings,
            ref CharacterRigidTransform rigidTransform,
            ref JumpState jumpState,
            ref JumpInputComponent jumpInput,
            in MovementInputComponent movementInput,
            in Vector3 viewerForward,
            in Vector3 viewerRight,
            int physicsTick)
        {
            jumpState.JustJumped = false;

            // Cannot jump on steep slopes unless stuck
            bool isGrounded = rigidTransform.IsGrounded && (!rigidTransform.IsOnASteepSlope || rigidTransform.IsStuck);

            if (isGrounded)
            {
                jumpState.LastGroundedTick = physicsTick;
                jumpState.JumpCount = 0;
                jumpState.AirJumpDelay = float.MinValue;
            }

            // Handle the air jumps delay first, in that state we can't do other jumps, we need to wait it out
            if (AwaitAirJumpDelay(settings, rigidTransform, ref jumpState, movementInput, viewerForward, viewerRight)) return;

            // The bonus frames are used for BOTH input buffering and coyote time
            int bonusFrames = ComputeBonusFrames(settings, rigidTransform, physicsTick);

            if (!isGrounded && !jumpState.IsCoyoteTimeActive(physicsTick, bonusFrames))
            {
                // Not grounded and coyote time expired
                // Set jump state to count as if we jumped at least once, so that we can't air jump more than allowed
                jumpState.JumpCount = Mathf.Max(jumpState.JumpCount, 1);
            }

            bool canJump = CanJump(settings, rigidTransform, jumpState, jumpInput, physicsTick, bonusFrames);
            bool wantsToJump = jumpInput.Trigger.IsAvailable(physicsTick, bonusFrames);
            if (canJump && wantsToJump) StartJump(settings, rigidTransform, ref jumpState, ref jumpInput, movementInput, physicsTick);
        }

        private static int ComputeBonusFrames(ICharacterControllerSettings settings, CharacterRigidTransform rigidTransform, int physicsTick)
        {
            int bonusFrames = Mathf.RoundToInt(settings.JumpGraceTime / UnityEngine.Time.fixedDeltaTime);

            // Reset the input buffering / coyote time windows if
            // - Positive Y velocity, so we already jumped
            // - The simulation just started, and we are within the input buffering window (otherwise the character will jump on its own)
            if (rigidTransform.GravityVelocity.y > 0 || physicsTick < bonusFrames) return 0;

            return bonusFrames;
        }

        private static bool CanJump(ICharacterControllerSettings settings,
            CharacterRigidTransform rigidTransform,
            in JumpState jumpState,
            in JumpInputComponent jumpInput,
            int physicsTick,
            int coyoteTimeTickCount)
        {
            bool isFirstJump = jumpState.JumpCount == 0;
            bool isGroundedOrCoyote = rigidTransform.IsGrounded || jumpState.IsCoyoteTimeActive(physicsTick, coyoteTimeTickCount);

            // Ensure the player is grounded if it's the 1st jump, otherwise just don't exceed max number of jumps
            // For air jumps we use a min value of 1 for jump count, that way falling down is considered as having jumped once
            bool canJump = (isFirstJump && isGroundedOrCoyote) || Mathf.Max(jumpState.JumpCount, 1) <= jumpState.MaxAirJumpCount;

            // Enforce the cooldown period between jumps
            if (!isFirstJump)
            {
                float timeSinceJumpStarted = (physicsTick - jumpInput.Trigger.TickWhenJumpWasConsumed) * UnityEngine.Time.fixedDeltaTime;
                canJump &= timeSinceJumpStarted >= settings.CooldownBetweenJumps;
            }

            return canJump;
        }

        private static void StartJump(ICharacterControllerSettings settings,
            CharacterRigidTransform rigidTransform,
            ref JumpState jumpState,
            ref JumpInputComponent jumpInput,
            in MovementInputComponent movementInput,
            int physicsTick)
        {
            if (jumpState.JumpCount <= 0)
                ApplyJumpGravity(settings, rigidTransform, jumpState, movementInput);
            else
                // Air jump, do not jump yet, just set the delay
                jumpState.AirJumpDelay = settings.AirJumpDelay;

            rigidTransform.IsGrounded = false;

            jumpState.JustJumped = true;
            jumpState.JumpCount++;

            jumpInput.Trigger.TickWhenJumpOccurred = int.MinValue;
            jumpInput.Trigger.TickWhenJumpWasConsumed = physicsTick;
        }

        private static void ApplyJumpGravity(ICharacterControllerSettings settings,
            CharacterRigidTransform rigidTransform,
            in JumpState jumpState,
            in MovementInputComponent movementInput)
        {
            float jumpHeight = GetJumpHeight(settings, jumpState, movementInput, rigidTransform.MoveVelocity.Velocity);
            float gravity = settings.Gravity * settings.JumpGravityFactor;

            // Override velocity in a jump direction
            rigidTransform.GravityVelocity.y = Mathf.Sqrt(-2 * jumpHeight * gravity);
        }

        private static float GetJumpHeight(ICharacterControllerSettings settings,
            in JumpState jumpState,
            in MovementInputComponent movementInput,
            in Vector3 flatHorizontalVelocity)
        {
            if (jumpState.JumpCount > 0) return settings.AirJumpHeight;

            float minJumpHeight = settings.JogJumpHeight;
            float maxJumpHeight = movementInput.Kind switch
                                  {
                                      MovementKind.WALK => settings.JogJumpHeight,
                                      MovementKind.JOG => settings.JogJumpHeight,
                                      MovementKind.IDLE => settings.JogJumpHeight,
                                      MovementKind.RUN => settings.RunJumpHeight,
                                      _ => throw new ArgumentOutOfRangeException(),
                                  };

            float currentSpeed = flatHorizontalVelocity.magnitude;
            return Mathf.Lerp(minJumpHeight, maxJumpHeight, currentSpeed / settings.RunSpeed);
        }

        /// <summary>
        ///     Awaits the air jump delay period, at the end of which the direction change impulse is applied.
        ///     During the await period gravity is overridden.
        ///     Returns true while still awaiting the delay period, false otherwise.
        /// </summary>
        private static bool AwaitAirJumpDelay(ICharacterControllerSettings settings,
            CharacterRigidTransform rigidTransform,
            ref JumpState jumpState,
            in MovementInputComponent movementInput,
            in Vector3 viewerForward,
            in Vector3 viewerRight)
        {
            if (jumpState.AirJumpDelay <= 0) return false;

            jumpState.AirJumpDelay -= UnityEngine.Time.fixedDeltaTime;
            if (jumpState.AirJumpDelay > 0)
            {
                // Still awaiting the delay period, just apply the gravity override
                // Setting the multiplier to 0 prevents standard gravity calculations to affect the value
                rigidTransform.GravityVelocity.y = settings.AirJumpGravityDuringDelay;
                rigidTransform.GravityMultiplier = 0;
                return true;
            }

            jumpState.AirJumpDelay = float.MinValue;

            ApplyJumpGravity(settings, rigidTransform, jumpState, movementInput);
            ApplyDirectionChangeImpulse(settings, rigidTransform, movementInput, viewerForward, viewerRight);

            return false;
        }

        private static void ApplyDirectionChangeImpulse(ICharacterControllerSettings settings,
            CharacterRigidTransform rigidTransform,
            in MovementInputComponent movementInput,
            in Vector3 viewerForward,
            in Vector3 viewerRight)
        {
            float impulse = Mathf.Max(settings.AirJumpDirectionChangeImpulse, rigidTransform.MoveVelocity.Velocity.magnitude);
            var localVelocity = impulse * movementInput.Axes;

            rigidTransform.MoveVelocity.XVelocity = localVelocity.x;
            rigidTransform.MoveVelocity.ZVelocity = localVelocity.y;
            rigidTransform.MoveVelocity.Velocity = (viewerForward * localVelocity.y) + (viewerRight * localVelocity.x);
        }
    }
}
