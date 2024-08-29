using DCL.CharacterMotion.Components;
using DCL.CharacterMotion.Settings;
using System.Runtime.CompilerServices;

namespace DCL.CharacterMotion.Utils
{
    public static class SpeedLimit
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Get(ICharacterControllerSettings settings, MovementKind movementKind)
        {
            switch (movementKind)
            {
                case MovementKind.RUN:
                    return settings.RunSpeed;
                case MovementKind.JOG:
                    return settings.JogSpeed;
                default: return settings.WalkSpeed;
            }
        }
    }
}
