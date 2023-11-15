using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
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
            if (!characterPhysics.IsGrounded)
            {
                float gravity = settings.Gravity;

                if (jumpInputComponent.IsPressed && PhysicsToDeltaTime(physicsTick - jumpInputComponent.Trigger.TickWhenJumpOccurred) < settings.LongJumpTime)
                    gravity *= settings.LongJumpGravityScale;

                if (characterPhysics.NonInterpolatedVelocity.y > 0)
                    gravity *= settings.JumpGravityFactor;

                // Gravity is already negative
                characterPhysics.NonInterpolatedVelocity += Vector3.up * gravity * deltaTime;
            }
            else

                // Gravity should always affect the character, otherwise we are unable to ground it properly
                characterPhysics.NonInterpolatedVelocity.y = settings.Gravity * deltaTime;
        }

        private static float PhysicsToDeltaTime(int ticks) =>
            UnityEngine.Time.fixedDeltaTime * ticks;
    }
}
