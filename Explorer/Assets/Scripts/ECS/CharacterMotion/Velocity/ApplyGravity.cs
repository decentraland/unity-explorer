using ECS.CharacterMotion.Components;
using ECS.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ECS.CharacterMotion
{
    public static class ApplyGravity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ICharacterControllerSettings characterControllerSettings,
            ref CharacterRigidTransform.Physics characterPhysics,
            float deltaTime)
        {
            if (!characterPhysics.IsGrounded)

                // Gravity is already negative
                characterPhysics.Velocity += Vector3.up * characterControllerSettings.Gravity * deltaTime;
            else
                characterPhysics.Velocity.y = 0f;
        }
    }
}
