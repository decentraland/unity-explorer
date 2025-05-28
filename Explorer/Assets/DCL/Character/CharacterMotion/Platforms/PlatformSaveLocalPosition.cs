using DCL.CharacterMotion.Components;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion.Platforms
{
    public static class PlatformSaveLocalPosition
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ref CharacterPlatformComponent platformComponent, Vector3 characterPosition)
        {
            if (platformComponent.CurrentPlatform != null)
            {
                Transform transform = platformComponent.CurrentPlatform.transform;
                Vector3 currentPlatformPosition = transform.position;

                if (platformComponent.LastPlatformPosition != null)
                {
                    platformComponent.LastPlatformDelta = platformComponent.LastPlatformPosition - currentPlatformPosition;
                    platformComponent.IsMovingPlatform = platformComponent.LastPlatformDelta.Value.sqrMagnitude > Mathf.Epsilon;
                }

                platformComponent.LastPlatformPosition = currentPlatformPosition;

                platformComponent.LastAvatarRelativePosition = transform.InverseTransformPoint(characterPosition);
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
