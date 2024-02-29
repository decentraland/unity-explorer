using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.ExternalUrlPrompt;
using DCL.TeleportPrompt;
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
        private readonly IGlobalWorldActions globalWorldActions;
        private readonly ISceneData sceneData;

        public RestrictedActionsAPIImplementation(
            IMVCManager mvcManager,
            ISceneStateProvider sceneStateProvider,
            IGlobalWorldActions globalWorldActions,
            ISceneData sceneData)
        {
            this.mvcManager = mvcManager;
            this.sceneStateProvider = sceneStateProvider;
            this.globalWorldActions = globalWorldActions;
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

            globalWorldActions.MoveAndRotatePlayer(newAbsolutePosition, newAbsoluteCameraTarget);
            globalWorldActions.RotateCamera(newAbsoluteCameraTarget, newAbsolutePosition);
        }

        public void TeleportTo(Vector2Int coords)
        {
            if (!sceneStateProvider.IsCurrent)
            {
                ReportHub.LogError(ReportCategory.RESTRICTED_ACTIONS, "TeleportTo: Player is not inside of scene");
                return;
            }

            TeleportAsync(coords).Forget();
        }

        private async UniTask OpenUrlAsync(string url)
        {
            await UniTask.SwitchToMainThread();
            await mvcManager.ShowAsync(ExternalUrlPromptController.IssueCommand(new ExternalUrlPromptController.Params(url)));
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

        private async UniTask TeleportAsync(Vector2Int coords)
        {
            await UniTask.SwitchToMainThread();
            await mvcManager.ShowAsync(TeleportPromptController.IssueCommand(new TeleportPromptController.Params(coords)));
        }

        public void Dispose() { }
    }
}
