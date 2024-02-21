using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.CharacterCamera;
using DCL.CharacterMotion.Components;
using DCL.Diagnostics;
using DCL.ExternalUrlPrompt;
using JetBrains.Annotations;
using MVC;
using SceneRunner.Scene;
using SceneRuntime.Apis.Modules;
using UnityEngine;
using Utility;

namespace CrdtEcsBridge.RestrictedActions
{
    public class RestrictedActionsAPIImplementation : IRestrictedActionsAPI
    {
        private readonly IMVCManager mvcManager;
        private readonly ISceneStateProvider sceneStateProvider;
        [CanBeNull] private readonly World world;
        private readonly Entity? playerEntity;
        private readonly ISceneData sceneData;

        public RestrictedActionsAPIImplementation(
            IMVCManager mvcManager,
            ISceneStateProvider sceneStateProvider,
            [CanBeNull] World world,
            Entity? playerEntity,
            ISceneData sceneData)
        {
            this.mvcManager = mvcManager;
            this.sceneStateProvider = sceneStateProvider;
            this.world = world;
            this.playerEntity = playerEntity;
            this.sceneData = sceneData;
        }

        public bool OpenExternalUrl(string url)
        {
            if (!sceneStateProvider.IsCurrent)
            {
                ReportHub.LogError(ReportCategory.RESTRICTED_ACTIONS, "OpenExternalUrl: Player is not inside of scene");
                return false;
            }

            OpenUrlAsync(url).Forget();
            return true;
        }

        public void MovePlayerTo(Vector3 newRelativePosition, Vector3? cameraTarget)
        {
            if (!sceneStateProvider.IsCurrent)
            {
                ReportHub.LogError(ReportCategory.RESTRICTED_ACTIONS, "MovePlayerTo: Player is not inside of scene");
                return;
            }

            Vector3 newAbsolutePosition = sceneData.Geometry.BaseParcelPosition + newRelativePosition;
            Vector3? newAbsoluteCameraTarget = cameraTarget != null ? sceneData.Geometry.BaseParcelPosition + cameraTarget.Value : null;

            if (!IsPositionValid(newAbsolutePosition))
            {
                ReportHub.LogError(ReportCategory.RESTRICTED_ACTIONS, "MovePlayerTo: Position is out of scene");
                return;
            }

            MoveAndRotatePlayer(newAbsolutePosition, newAbsoluteCameraTarget);
            RotateCamera(newAbsoluteCameraTarget, newAbsolutePosition);
        }

        private async UniTask OpenUrlAsync(string url)
        {
            await UniTask.SwitchToMainThread();
            await mvcManager.ShowAsync(ExternalUrlPromptController.IssueCommand(new ExternalUrlPromptController.Params(url)));
        }

        private void MoveAndRotatePlayer(Vector3 newPlayerPosition, Vector3? newCameraTarget)
        {
            if (playerEntity == null || world == null)
                return;

            // Move player to new position (through InterpolateCharacterSystem -> MovePlayerQuery)
            world.Add(playerEntity.Value, new PlayerMoveIntent(newPlayerPosition));

            // Rotate player to look at camera target (through RotateCharacterSystem -> ForceLookAtQuery)
            if (newCameraTarget != null)
                world.Add(playerEntity.Value, new PlayerLookAtIntent(newCameraTarget.Value));
        }

        private void RotateCamera(Vector3? newCameraTarget, Vector3 newPlayerPosition)
        {
            if (newCameraTarget == null || world == null)
                return;

            // Rotate camera to look at new target (through ApplyCinemachineCameraInputSystem -> ForceLookAtQuery)
            var camera = world.CacheCamera();
            world.Add(camera, new CameraLookAtIntent(newCameraTarget.Value, newPlayerPosition));
        }

        private bool IsPositionValid(Vector3 floorPosition)
        {
            var parcelToCheck = ParcelMathHelper.FloorToParcel(floorPosition);
            foreach (Vector2Int sceneParcel in sceneData.Parcels)
            {
                if (sceneParcel == parcelToCheck)
                    return true;
            }

            return false;
        }

        public void Dispose() { }
    }
}
