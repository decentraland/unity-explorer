using DCL.Character.CharacterMotion.Components;
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
            Vector3 gravityDirection = characterPhysics.IsOnASteepSlope ? characterPhysics.GravityDirection : Vector3.down;

            // when grounded and on a steep slope, we tilt the gravity towards the slope downwards direction using the current magnitude to maintain momentum
            if (characterPhysics.IsOnASteepSlope && characterPhysics.IsGrounded && !characterPhysics.IsStuck)
            {
                float currentGravityMagnitude = characterPhysics.GravityVelocity.magnitude;
                characterPhysics.SlopeGravity = gravityDirection * currentGravityMagnitude;
            }

            // If we are falling
            if (!characterPhysics.IsGrounded || characterPhysics.JustJumped || (characterPhysics.IsOnASteepSlope && !characterPhysics.IsStuck))
            {
                // gravity in settings is negative, since we now use directions, we need it to be absolute
                float gravity = Math.Abs(settings.Gravity);

                // In order to jump higher when pressing the jump button, we reduce the gravity
                if (jumpInputComponent.IsPressed && PhysicsToDeltaTime(physicsTick - jumpInputComponent.Trigger.TickWhenJumpWasConsumed) < settings.LongJumpTime)
                    gravity *= settings.LongJumpGravityScale;

                // In order to feel less floaty when jumping, we increase the gravity when going up ( the jump velocity is also scaled up )
                if (characterPhysics.GravityVelocity.y > 0)
                    gravity *= settings.JumpGravityFactor;

                characterPhysics.GravityVelocity += gravityDirection * gravity * deltaTime;
                characterPhysics.SlopeGravity += gravityDirection * gravity * deltaTime;
            }
            else
            {
                // Gravity should always affect the character, otherwise we are unable to ground it properly
                characterPhysics.GravityVelocity = gravityDirection * Math.Abs(settings.Gravity) * deltaTime;
                characterPhysics.SlopeGravity = characterPhysics.GravityVelocity;
            }
        }

        private static float PhysicsToDeltaTime(int ticks) =>
            UnityEngine.Time.fixedDeltaTime * ticks;
    }
}
