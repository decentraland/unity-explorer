using DCL.CharacterMotion.Components;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion.Platforms
{
    public static class PlatformSaveLocalRotation
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ref CharacterPlatformComponent platformComponent, Vector3 forward)
        {
            if (platformComponent.CurrentPlatform != null)
                platformComponent.LastAvatarRelativeRotation =
                    platformComponent.CurrentPlatform.transform.InverseTransformDirection(forward);
        }
    }
}
