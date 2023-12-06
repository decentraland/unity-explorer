using DCL.CharacterMotion.Components;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion.Platforms
{
    public static class PlatformSaveLocalPosition
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ref CharacterPlatformComponent platformComponent, Vector3 position)
        {
            if (platformComponent.CurrentPlatform != null)
                platformComponent.LastPosition = platformComponent.CurrentPlatform.transform.InverseTransformPoint(position);
        }
    }
}
