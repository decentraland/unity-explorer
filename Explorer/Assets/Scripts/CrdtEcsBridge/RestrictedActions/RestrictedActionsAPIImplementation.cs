using Cysharp.Threading.Tasks;
using DCL.ChangeRealmPrompt;
using DCL.Diagnostics;
using DCL.ExternalUrlPrompt;
using DCL.NftPrompt;
using DCL.TeleportPrompt;
using DCL.Utilities;
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

        public bool TryOpenExternalUrl(string url)
        {
            if (!sceneStateProvider.IsCurrent)
                return false;

            OpenUrlAsync(url).Forget();
            return true;
        }

        public void TryMovePlayerTo(Vector3 newRelativePosition, Vector3? cameraTarget)
        {
            if (!sceneStateProvider.IsCurrent)
                return;

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

        public void TryTeleportTo(Vector2Int coords)
        {
            if (!sceneStateProvider.IsCurrent)
                return;

            TeleportAsync(coords).Forget();
        }

        public bool TryChangeRealm(string message, string realm)
        {
            if (!sceneStateProvider.IsCurrent)
                return false;

            ChangeRealmAsync(message, realm).Forget();
            return true;
        }

        public void TryTriggerEmote(string predefinedEmote)
        {
            if (!sceneStateProvider.IsCurrent)
                return;

            // TODO: Implement emote triggering (blocked until emotes are implemented)...
        }

        public bool TryTriggerSceneEmote(string src, bool loop)
        {
            if (!sceneStateProvider.IsCurrent)
                return false;

            // TODO: Implement scene emote triggering (blocked until emotes are implemented)...

            return true;
        }

        public bool TryOpenNftDialog(string urn)
        {
            if (!sceneStateProvider.IsCurrent)
                return false;

            if (!NftUtils.TryParseUrn(urn, out string contractAddress, out string tokenId))
            {
                ReportHub.LogError(ReportCategory.RESTRICTED_ACTIONS, $"OpenNftDialog: Urn '{urn}' is not valid");
                return false;
            }

            OpenNftDialogAsync(contractAddress, tokenId).Forget();
            return true;

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

        private async UniTask ChangeRealmAsync(string message, string realm)
        {
            await UniTask.SwitchToMainThread();
            await mvcManager.ShowAsync(ChangeRealmPromptController.IssueCommand(new ChangeRealmPromptController.Params(message, realm)));
        }

        private async UniTask OpenNftDialogAsync(string contractAddress, string tokenId)
        {
            await UniTask.SwitchToMainThread();
            await mvcManager.ShowAsync(NftPromptController.IssueCommand(new NftPromptController.Params(contractAddress, tokenId)));
        }

        public void Dispose() { }
    }
}
