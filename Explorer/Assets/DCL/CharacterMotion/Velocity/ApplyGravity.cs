using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion
{
    public static class ApplyGravity
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ICharacterControllerSettings characterControllerSettings,
            ref CharacterRigidTransform characterPhysics,
            float deltaTime)
        {
            if (!characterPhysics.IsGrounded)

                // Gravity is already negative
                characterPhysics.NonInterpolatedVelocity += Vector3.up * characterControllerSettings.Gravity * deltaTime;
            else

                // Gravity should always affect the character, otherwise we are unable to ground it properly
                characterPhysics.NonInterpolatedVelocity.y = characterControllerSettings.Gravity * deltaTime;
        }
    }
}
