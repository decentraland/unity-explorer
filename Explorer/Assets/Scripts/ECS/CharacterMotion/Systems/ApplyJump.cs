using ECS.CharacterMotion.Components;
using ECS.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace ECS.CharacterMotion.Systems
{
    public static class ApplyJump
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(
            ICharacterControllerSettings characterControllerSettings,
            ref JumpInputComponent jump,
            ref CharacterPhysics characterPhysics,
            int physicsTick)
        {
            float power = jump.PhysicalButtonArguments.GetPower(physicsTick);
            if (characterPhysics.IsGrounded && power > 0)
            {
                float jumpHeight = Mathf.Lerp(characterControllerSettings.JumpHeight.x, characterControllerSettings.JumpHeight.y, power);

                // Override velocity in a jump direction
                characterPhysics.Velocity.y = Mathf.Sqrt(-2 * jumpHeight * characterControllerSettings.Gravity);

                characterPhysics.IsGrounded = false;
            }
        }
    }
}
