using Arch.Core;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using UnityEngine;

namespace CrdtEcsBridge.RestrictedActions
{
    public class GlobalWorldActions : IGlobalWorldActions
    {
        private readonly World world;
        private readonly Entity playerEntity;

        public GlobalWorldActions(
            World world,
            Entity playerEntity)
        {
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public void MoveAndRotatePlayer(Vector3 newPlayerPosition, Vector3? newCameraTarget)
        {
            // Move player to new position (through InterpolateCharacterSystem -> TeleportPlayerQuery)
            world.Add(playerEntity, new PlayerTeleportIntent(newPlayerPosition, Vector2Int.zero, null));

            // Rotate player to look at camera target (through RotateCharacterSystem -> ForceLookAtQuery)
            if (newCameraTarget != null)
                world.Add(playerEntity, new PlayerLookAtIntent(newCameraTarget.Value));
        }

        public void RotateCamera(Vector3? newCameraTarget, Vector3 newPlayerPosition)
        {
            if (newCameraTarget == null || world == null)
                return;

            // Rotate camera to look at new target (through ApplyCinemachineCameraInputSystem -> ForceLookAtQuery)
            var camera = world.CacheCamera();
            world.Add(camera, new CameraLookAtIntent(newCameraTarget.Value, newPlayerPosition));
        }
    }
}
