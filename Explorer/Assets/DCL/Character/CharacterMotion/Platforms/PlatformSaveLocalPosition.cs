using DCL.CharacterMotion.Components;
using SceneRunner.Scene;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace DCL.CharacterMotion.Platforms
{
    public static class PlatformSaveLocalPosition
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Execute(ref CharacterPlatformComponent platformComponent, Vector3 characterPosition, ISceneFacade? currentScene)
        {
            Transform? platform = platformComponent.CurrentPlatform;

            if (!platform)
            {
                platformComponent.ClearPositionState();
                return;
            }

            platformComponent.LastAvatarRelativePosition = platform.InverseTransformPoint(characterPosition);

            // We only update the changed flag if a scene tick happened since last update
            // We are assuming that to hit something, that game object must be part of the current scene
            if (currentScene != null && currentScene.SceneStateProvider.TickNumber > platformComponent.LastUpdateTick)
            {
                Vector3 updatedPosition = platform.position;

                if (platformComponent.LastPlatformPosition != null)
                {
                    Vector3 positionDelta = platformComponent.LastPlatformPosition.Value - updatedPosition;
                    platformComponent.PositionChanged = positionDelta.sqrMagnitude > Mathf.Epsilon;
                }

                platformComponent.LastPlatformPosition = updatedPosition;
            }
        }
    }
}
