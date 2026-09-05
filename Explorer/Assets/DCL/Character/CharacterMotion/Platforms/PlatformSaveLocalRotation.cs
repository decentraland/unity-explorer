using DCL.CharacterMotion.Components;
using SceneRunner.Scene;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion.Platforms
{
    public static class PlatformSaveLocalRotation
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ref CharacterPlatformComponent platformComponent, Vector3 forward, ISceneFacade? currentScene)
        {
            Transform? platform = platformComponent.CurrentPlatform;

            if (!platform)
            {
                platformComponent.ClearRotationState();
                return;
            }

            platformComponent.LastAvatarRelativeRotation = platform.InverseTransformDirection(forward);

            // We only update the changed flag if a scene tick happened since last update
            // We are assuming that to hit something, that game object must be part of the current scene
            if (currentScene != null && currentScene.SceneStateProvider.TickNumber > platformComponent.LastUpdateTick)
            {
                var updatedRotation = platform.rotation;

                if (platformComponent.LastPlatformRotation != null)
                {
                    float rotationDelta = Quaternion.Angle(platformComponent.LastPlatformRotation.Value, updatedRotation);
                    platformComponent.RotationChanged = rotationDelta > Mathf.Epsilon;
                }

                platformComponent.LastPlatformRotation = updatedRotation;
            }
        }
    }
}
