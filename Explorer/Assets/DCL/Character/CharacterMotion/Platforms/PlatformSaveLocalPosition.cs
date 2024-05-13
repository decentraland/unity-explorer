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
            {
                Transform transform = platformComponent.CurrentPlatform.transform;
                Vector3 currentPlatformPosition = transform.position;

                if (platformComponent.LastPlatformPosition != null)
                {
                    platformComponent.LastPlatformDelta = platformComponent.LastPlatformPosition - currentPlatformPosition;

                    if (platformComponent.LastPlatformDelta.Value.sqrMagnitude > Mathf.Epsilon)
                        platformComponent.IsMovingPlatform = true;
                }

                platformComponent.LastPlatformPosition = currentPlatformPosition;
                platformComponent.LastAvatarRelativePosition = transform.InverseTransformPoint(position);
            }
            else
            {
                platformComponent.LastPlatformPosition = null;
                platformComponent.LastPlatformDelta = null;
                platformComponent.IsMovingPlatform = false;
            }
        }
    }
}
