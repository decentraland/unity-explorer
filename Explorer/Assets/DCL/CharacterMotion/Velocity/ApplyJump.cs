using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion
{
    public static class ApplyJump
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(
            ICharacterControllerSettings characterControllerSettings,
            ref JumpInputComponent jump,
            ref CharacterRigidTransform characterPhysics,
            int physicsTick)
        {
            float power = jump.PhysicalButtonArguments.GetPower(physicsTick);

            if (characterPhysics.IsGrounded && power > 0)
            {
                float jumpHeight = Mathf.Lerp(characterControllerSettings.JumpHeight.x, characterControllerSettings.JumpHeight.y, power);

                // Override velocity in a jump direction
                characterPhysics.NonInterpolatedVelocity.y = Mathf.Sqrt(-2 * jumpHeight * characterControllerSettings.Gravity);

                characterPhysics.IsGrounded = false;
            }
        }
    }
}
