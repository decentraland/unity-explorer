﻿using DCL.CharacterMotion.Components;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion.Platforms
{
    public static class PlatformSaveLocalRotation
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ref CharacterPlatformComponent platformComponent, Vector3 forward)
        {
            if (platformComponent.CurrentPlatform.Has)
            {
                Transform transform = platformComponent.CurrentPlatform.Value.Transform;
                var currentPlatformRotation = transform.rotation;

                if (platformComponent.LastPlatformRotation != null)
                {
                    float angleDifference = Quaternion.Angle(platformComponent.LastPlatformRotation.Value, currentPlatformRotation);

                    platformComponent.IsRotatingPlatform = angleDifference > Mathf.Epsilon;
                }

                platformComponent.LastPlatformRotation = currentPlatformRotation;
                platformComponent.LastAvatarRelativeRotation = platformComponent.CurrentPlatform.Value.Transform.InverseTransformDirection(forward);
            }
            else
            {
                platformComponent.LastPlatformRotation = null;
                platformComponent.IsRotatingPlatform = false;
            }
        }
    }
}
