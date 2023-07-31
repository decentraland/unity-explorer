using ECS.CharacterMotion.Components;
using ECS.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ECS.CharacterMotion.Systems
{
    public static class ApplyGravity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ICharacterControllerSettings characterControllerSettings,
            ref CharacterPhysics characterPhysics,
            float deltaTime)
        {
            if (!characterPhysics.IsGrounded)
                characterPhysics.Velocity += Vector3.down * characterControllerSettings.Gravity * deltaTime;
        }
    }
}
