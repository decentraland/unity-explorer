using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion
{
    public static class ApplyGravity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ICharacterControllerSettings settings,
            ref CharacterRigidTransform characterPhysics,
            in JumpInputComponent jumpInputComponent,
            int physicsTick,
            float deltaTime)
        {
            Vector3 gravityDirection = characterPhysics.GravityDirection;

            // when grounded and on a steep slope, we tilt the gravity towards the slope downwards direction using the current magnitude to maintain momentum
            if (characterPhysics.IsOnASteepSlope && characterPhysics.IsGrounded)
            {
                float currentGravityMagnitude = characterPhysics.GravityVelocity.magnitude;
                characterPhysics.GravityVelocity = gravityDirection * currentGravityMagnitude;
            }

            if (!characterPhysics.IsGrounded || characterPhysics.IsOnASteepSlope)
            {
                // gravity in settings is negative, since we now use directions, we need it positive
                float gravity = Math.Abs(settings.Gravity);

                if (jumpInputComponent.IsPressed && PhysicsToDeltaTime(physicsTick - jumpInputComponent.Trigger.TickWhenJumpOccurred) < settings.LongJumpTime)
                    gravity *= settings.LongJumpGravityScale;

                if (characterPhysics.GravityVelocity.y > 0)
                    gravity *= settings.JumpGravityFactor;

                characterPhysics.GravityVelocity += gravityDirection * gravity * deltaTime;
            }
            else
            {
                // Gravity should always affect the character, otherwise we are unable to ground it properly
                characterPhysics.GravityVelocity = gravityDirection * Math.Abs(settings.Gravity) * deltaTime;
            }
        }

        private static float PhysicsToDeltaTime(int ticks) =>
            UnityEngine.Time.fixedDeltaTime * ticks;
    }
}
