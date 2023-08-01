using ECS.CharacterMotion.Components;
using ECS.CharacterMotion.Settings;
using System.Runtime.CompilerServices;

namespace ECS.CharacterMotion.Systems
{
    public static class ApplyAirDrag
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ICharacterControllerSettings characterControllerSettings,
            ref CharacterPhysics characterPhysics,
            float deltaTime)
        {
            if (!characterPhysics.IsGrounded)
                characterPhysics.Velocity *= 1f / (1f + (characterControllerSettings.AirDrag * deltaTime));
        }
    }
}
