using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;

namespace DCL.CharacterMotion.Utils
{
    public static class MovementSpeedLimitHelper
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetMovementSpeedLimit(ICharacterControllerSettings settings, MovementKind movementKind) =>
            movementKind switch
            {
                MovementKind.RUN => settings.RunSpeed,
                MovementKind.JOG => settings.JogSpeed,
                _ => settings.WalkSpeed
            };

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetAnimationBlendingSpeedLimit(ICharacterControllerSettings settings, MovementKind movementKind) =>
            movementKind switch
            {
                MovementKind.RUN => settings.MovAnimBlendMaxRunSpeed,
                MovementKind.JOG => settings.MovAnimBlendMaxJogSpeed,
                _ => settings.MovAnimBlendMaxWalkSpeed
            };
    }
}
