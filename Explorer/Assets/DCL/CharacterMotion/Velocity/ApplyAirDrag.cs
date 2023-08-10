using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;

namespace DCL.CharacterMotion
{
    public static class ApplyAirDrag
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ICharacterControllerSettings characterControllerSettings,
            ref CharacterRigidTransform characterPhysics,
            float deltaTime)
        {
            if (!characterPhysics.IsGrounded)
                characterPhysics.NonInterpolatedVelocity *= 1f / (1f + (characterControllerSettings.AirDrag * deltaTime));
        }
    }
}
