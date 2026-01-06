using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using DCL.CharacterMotion.Utils;
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
            Vector3 forward,
            in MovementInputComponent inputComponent,
            int physicsTick)
        {
            characterPhysics.JustJumped = false;

            bool isJumpDisabled = characterPhysics.IsOnASteepSlope && !characterPhysics.IsStuck;

            // update the grounded frame from last frame
            if (characterPhysics.IsGrounded && !isJumpDisabled)
            {
                characterPhysics.LastGroundedFrame = physicsTick;
                characterPhysics.JumpCount = 0;
            }

            // (Coyote Timer: Pressing Jump before touching ground)
            // We calculate the bonus frames that we have after we decide to jump, settings.JumpGraceTime is in seconds, we convert it into physics ticks
            int bonusFrames = Mathf.RoundToInt(settings.JumpGraceTime / UnityEngine.Time.fixedDeltaTime);

            // no bonus frames if we are already going up
            if (characterPhysics.GravityVelocity.y > 0)
                bonusFrames = 0;

            // avoid triggering jump on the first frames
            if (physicsTick < bonusFrames)
                bonusFrames = 0;

            bool wantsToJump = jump.Trigger.IsAvailable(physicsTick, bonusFrames);

            const int MAX_JUMP_COUNT = 2;
            bool isFirstJump = characterPhysics.JumpCount == 0;
            bool tooManyJumps = characterPhysics.JumpCount >= MAX_JUMP_COUNT;
            float timeSinceJumpStarted = (physicsTick - jump.Trigger.TickWhenJumpWasConsumed) * UnityEngine.Time.fixedDeltaTime;
            bool canJump = (isFirstJump && (characterPhysics.IsGrounded || physicsTick - characterPhysics.LastGroundedFrame < bonusFrames))
                           || !tooManyJumps;
            if (!isFirstJump && timeSinceJumpStarted < 0.3f) canJump = false;

            // (Coyote Timer: Pressing Jump late after starting to fall, to give the player a chance to jump after not being grounded)

            if (canJump && wantsToJump && !isJumpDisabled)
            {
                if (!isFirstJump)
                {
                    const float SPEED = 8;
                    var velocity = SPEED * inputComponent.Axes;
                    characterPhysics.MoveVelocity.XVelocity = velocity.x;
                    characterPhysics.MoveVelocity.ZVelocity = velocity.y;
                    characterPhysics.MoveVelocity.Velocity = (forward * velocity.y) + (Vector3.Cross(-forward, Vector3.up) * velocity.x);
                }

                float jumpHeight = GetJumpHeight(characterPhysics.MoveVelocity.Velocity, settings, inputComponent);
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
        private static float GetJumpHeight(Vector3 flatHorizontalVelocity, ICharacterControllerSettings settings, in MovementInputComponent input)
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
            float jumpHeight = Mathf.Lerp(settings.JogJumpHeight, maxJumpHeight, currentSpeed / settings.RunSpeed);
            return jumpHeight;
        }
    }
}
