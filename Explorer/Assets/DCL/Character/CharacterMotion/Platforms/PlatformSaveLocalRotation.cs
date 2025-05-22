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
            {
                Transform transform = platformComponent.CurrentPlatform.transform;
                var currentPlatformRotation = transform.rotation;

                if (platformComponent.LastPlatformRotation != null)
                {
                    float angleDifference = Quaternion.Angle(platformComponent.LastPlatformRotation.Value, currentPlatformRotation);

                    if (angleDifference > Mathf.Epsilon)
                        platformComponent.IsRotatingPlatform = true;
                }

                platformComponent.LastPlatformRotation = currentPlatformRotation;
                platformComponent.LastAvatarRelativeRotation = platformComponent.CurrentPlatform.transform.InverseTransformDirection(forward);
            }
            else
            {
                platformComponent.LastPlatformRotation = null;
                platformComponent.IsRotatingPlatform = false;
            }
        }
    }
}
