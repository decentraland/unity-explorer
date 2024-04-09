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
            in MovementInputComponent inputComponent,
            int physicsTick)
        {
            characterPhysics.JustJumped = false;

            bool isJumpDisabled = characterPhysics.IsOnASteepSlope && !characterPhysics.IsStuck;

            // update the grounded frame from last frame
            if (characterPhysics.IsGrounded && !isJumpDisabled)
                characterPhysics.LastGroundedFrame = physicsTick;

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

            bool canJump = characterPhysics.IsGrounded || physicsTick - characterPhysics.LastGroundedFrame < bonusFrames;

            // (Coyote Timer: Pressing Jump late after starting to fall, to give the player a chance to jump after not being grounded)

            if (canJump && wantsToJump && !isJumpDisabled)
            {
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
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float GetJumpHeight(Vector3 flatHorizontalVelocity, ICharacterControllerSettings settings, in MovementInputComponent input)
        {
            float maxJumpHeight = input.Kind switch
                                  {
                                      MovementKind.Walk => settings.JogJumpHeight,
                                      MovementKind.Jog => settings.JogJumpHeight,
                                      MovementKind.Run => settings.RunJumpHeight,
                                      _ => throw new ArgumentOutOfRangeException(),
                                  };

            float currentSpeed = flatHorizontalVelocity.magnitude;
            float jumpHeight = Mathf.Lerp(settings.JogJumpHeight, maxJumpHeight, currentSpeed / settings.RunSpeed);
            return jumpHeight;
        }
    }
}
