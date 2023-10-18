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
            ICharacterControllerSettings characterControllerSettings,
            ref CharacterRigidTransform characterPhysics,
            in JumpInputComponent jump,
            in MovementInputComponent inputComponent,
            int physicsTick)
        {
            bool wantsToJump = jump.Trigger.IsAvailable(physicsTick);

            if (characterPhysics.IsGrounded && wantsToJump)
            {
                float jumpHeight = GetJumpHeight(characterPhysics.MoveVelocity.Velocity, characterControllerSettings, inputComponent);
                float gravity = characterControllerSettings.Gravity * characterControllerSettings.JumpGravityFactor;

                // Override velocity in a jump direction
                characterPhysics.NonInterpolatedVelocity.y = Mathf.Sqrt(-2 * jumpHeight * gravity);

                characterPhysics.IsGrounded = false;
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
