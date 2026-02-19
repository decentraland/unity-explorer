using DCL.Character.CharacterMotion.Components;
using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.Character.CharacterMotion
{
    public static class ApplyGravity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ICharacterControllerSettings settings, ref CharacterRigidTransform characterPhysics, in JumpInputComponent jumpInputComponent,
            int physicsTick, float deltaTime)
        {
            Vector3 gravityDirection = characterPhysics.IsOnASteepSlope ? characterPhysics.GravityDirection : Vector3.down;

            // when grounded and on a steep slope, we tilt the gravity towards the slope downwards direction using the current magnitude to maintain momentum
            if (characterPhysics is { IsGrounded: true, IsOnASteepSlope: true, IsStuck: false })
            {
                float currentGravityMagnitude = characterPhysics.GravityVelocity.magnitude;
                characterPhysics.SlopeGravity = gravityDirection * currentGravityMagnitude;
            }

            if (IsFalling(characterPhysics))
            {
                float gravity = Math.Abs(settings.Gravity); // gravity in settings is negative

                // To jump higher when pressing the jump button, we reduce the gravity
                if (jumpInputComponent.IsPressed && PhysicsToDeltaTime(physicsTick - jumpInputComponent.Trigger.TickWhenJumpWasConsumed) < settings.LongJumpTime)
                    gravity *= settings.LongJumpGravityScale;

                // To feel less floaty when jumping, we increase the gravity when going up (the jump velocity is also scaled up)
                if (characterPhysics.GravityVelocity.y > 0)
                    gravity *= settings.JumpGravityFactor;

                // Effective gravity: base gravity minus external vertical acceleration
                // When external force counteracts gravity (e.g., wind tunnel), effective gravity approaches zero
                float effectiveGravity = gravity - characterPhysics.ExternalAcceleration.y;

                characterPhysics.GravityVelocity += gravityDirection * (effectiveGravity * deltaTime);
                characterPhysics.SlopeGravity += gravityDirection * (effectiveGravity * deltaTime);
            }
            else // Grounded: compute effective gravity without jump modifiers
            {
                float effectiveGravity = Math.Abs(settings.Gravity) - characterPhysics.ExternalAcceleration.y;

                // Net force is upward — unground and start accumulating
                if (effectiveGravity <= 0f)
                    characterPhysics.IsGrounded = false;

                characterPhysics.GravityVelocity = gravityDirection * (effectiveGravity * deltaTime);
                characterPhysics.SlopeGravity = characterPhysics.GravityVelocity;
            }

            return;
            bool IsFalling(CharacterRigidTransform characterRigidTransform) =>
                !characterRigidTransform.IsGrounded || characterRigidTransform.JustJumped || characterRigidTransform is { IsOnASteepSlope: true, IsStuck: false };
        }

        private static float PhysicsToDeltaTime(int ticks) =>
            UnityEngine.Time.fixedDeltaTime * ticks;
    }
}
