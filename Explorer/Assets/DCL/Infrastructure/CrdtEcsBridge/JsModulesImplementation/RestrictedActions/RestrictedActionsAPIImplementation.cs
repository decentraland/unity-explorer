using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Emotes;
using DCL.ChangeRealmPrompt;
using DCL.Clipboard;
using DCL.CrdtEcsBridge.JsModulesImplementation;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.FeatureFlags;
using DCL.UI;
using Utility.Arch;
using DCL.ExternalUrlPrompt;
using DCL.NftPrompt;
using DCL.SceneRuntime.Apis.RestrictedActionsApi;
using DCL.TeleportPrompt;
using DCL.Utilities;
using Decentraland.Kernel.Apis;
using MVC;
using SceneRunner.Scene;
using SceneRuntime.ScenePermissions;
using System;
using System.Threading;
using UnityEngine;
using Utility;

namespace CrdtEcsBridge.RestrictedActions
{
    public class RestrictedActionsAPIImplementation : IRestrictedActionsAPI
    {
        private const uint USER_GESTURE_WINDOW_TICKS = 1;

        private readonly IMVCManager mvcManager;
        private readonly ISceneStateProvider sceneStateProvider;
        private readonly IGlobalWorldActions globalWorldActions;
        private readonly ISceneData sceneData;
        private readonly IJsApiPermissionsProvider permissionsProvider;
        private readonly ISystemClipboard systemClipboard;
        private readonly IExplorerUiActions explorerUiActions;
        private readonly Arch.Core.World sceneWorld;
        private readonly Arch.Core.Entity scenePlayerEntity;

        public RestrictedActionsAPIImplementation(
            IMVCManager mvcManager,
            ISceneStateProvider sceneStateProvider,
            IGlobalWorldActions globalWorldActions,
            ISceneData sceneData,
            IJsApiPermissionsProvider permissionsProvider,
            ISystemClipboard systemClipboard,
            Arch.Core.World sceneWorld,
            Arch.Core.Entity scenePlayerEntity,
            IExplorerUiActions explorerUiActions)
        {
            this.mvcManager = mvcManager;
            this.sceneStateProvider = sceneStateProvider;
            this.globalWorldActions = globalWorldActions;
            this.sceneData = sceneData;
            this.permissionsProvider = permissionsProvider;
            this.systemClipboard = systemClipboard;
            this.sceneWorld = sceneWorld;
            this.scenePlayerEntity = scenePlayerEntity;
            this.explorerUiActions = explorerUiActions;
        }

        public bool TryOpenExternalUrl(string url)
        {
            if (!permissionsProvider.CanOpenExternalUrl())
                return false;

            if (!sceneStateProvider.IsCurrent)
                return false;

            OpenUrlAsync(url).Forget();
            return true;
        }

        public async UniTask<bool> TryMovePlayerToAsync(Vector3 newRelativePosition, Vector3? cameraTarget, Vector3? avatarTarget, float duration, CancellationToken ct)
        {
            if (!sceneStateProvider.IsCurrent)
                return false;

            Vector3 newAbsolutePosition = sceneData.Geometry.BaseParcelPosition + newRelativePosition;
            Vector3? newAbsoluteCameraTarget = cameraTarget != null ? sceneData.Geometry.BaseParcelPosition + cameraTarget.Value : null;
            Vector3? newAbsoluteAvatarTarget = avatarTarget != null ? sceneData.Geometry.BaseParcelPosition + avatarTarget.Value : null;

            if (!IsPositionValid(newAbsolutePosition) && !sceneData.IsPortableExperience())
            {
                ReportHub.LogError(ReportCategory.RESTRICTED_ACTIONS, "MovePlayerTo: Position is out of scene");
                return false;
            }

            try
            {
                await UniTask.SwitchToMainThread();

                globalWorldActions.RotateCamera(newAbsoluteCameraTarget, newAbsolutePosition);
                return await globalWorldActions.MoveAndRotatePlayerAsync(newAbsolutePosition, newAbsoluteCameraTarget, newAbsoluteAvatarTarget, duration, ct);
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.RESTRICTED_ACTIONS, sceneShortInfo: sceneData.SceneShortInfo));
                return false;
            }
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

        public void TryTriggerEmote(string predefinedEmote, AvatarEmoteMask mask)
        {
            if (!sceneStateProvider.IsCurrent)
                return;

            if (mask == AvatarEmoteMask.AemFullBody)
                globalWorldActions.TriggerEmote(predefinedEmote, false, mask);
            else
                TriggerMaskedEmoteOnSceneWorld(predefinedEmote, mask);
        }

        public async UniTask<bool> TryTriggerSceneEmoteAsync(string src, bool loop, AvatarEmoteMask mask, CancellationToken ct)
        {
            if (!sceneStateProvider.IsCurrent)
                return false;

            if (!sceneData.SceneContent.TryGetHash(src, out string hash))
                return false;

            try
            {
                await UniTask.SwitchToMainThread();

                var resolved = await globalWorldActions.TriggerSceneEmoteAsync(sceneData, src, hash, loop, mask, ct);

                if (resolved is not var (urn, isLooping))
                    return false;

                if (mask == AvatarEmoteMask.AemFullBody)
                    globalWorldActions.TriggerEmote(urn, isLooping, mask);
                else
                    TriggerMaskedEmoteOnSceneWorld(urn, mask);
            }
            catch (OperationCanceledException) { return false; }
            catch (Exception e)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.EMOTE, sceneShortInfo: sceneData.SceneShortInfo));
                return false;
            }

            return true;
        }

        public void TryStopEmote()
        {
            if (!sceneStateProvider.IsCurrent)
                return;

            // Stop full-body emote on global world
            globalWorldActions.StopEmote();

            // Stop masked emote on this scene's world
            if (sceneWorld.TryGet(scenePlayerEntity, out CharacterMaskedEmoteComponent masked))
            {
                masked.StopEmote = true;
                masked.EmoteUrn = default; // Permanent stop — don't replay on re-entry
                sceneWorld.Set(scenePlayerEntity, masked);
            }
        }

        private void TriggerMaskedEmoteOnSceneWorld(CommunicationData.URLHelpers.URN urn, AvatarEmoteMask mask)
        {
            // Ensure the scene player entity has the masked emote component
            sceneWorld.AddOrGet(scenePlayerEntity, new CharacterMaskedEmoteComponent());

            // Create the intent on the scene world's player entity — SceneMaskedEmoteSystem will consume it
            sceneWorld.AddOrSet(scenePlayerEntity, new CharacterEmoteIntent
            {
                EmoteId = urn,
                Spatial = true,
                TriggerSource = TriggerSource.Scene,
                Mask = mask,
            });
        }

        public bool TryOpenNftDialog(string urn)
        {
            if (!sceneStateProvider.IsCurrent)
                return false;

            if (!NftUtils.TryParseUrn(urn, out string chain, out string contractAddress, out string tokenId))
            {
                ReportHub.LogError(ReportCategory.RESTRICTED_ACTIONS, $"OpenNftDialog: Urn '{urn}' is not valid");
                return false;
            }

            OpenNftDialogAsync(chain, contractAddress, tokenId).Forget();
            return true;
        }

        public int TryOpenExplorerUi(int ui)
        {
            if (!sceneStateProvider.IsCurrent)
                return (int)OpenExplorerUiResult.RejectedNotCurrentScene;

            // Accept only calls made within USER_GESTURE_WINDOW_TICKS of the last recorded pointer gesture.
            // The "== 0" guard is not redundant: 0 means "no input was ever recorded".
            uint lastUserInputTick = sceneStateProvider.LastUserInputTick;

            if (lastUserInputTick == 0 || lastUserInputTick + USER_GESTURE_WINDOW_TICKS < sceneStateProvider.TickNumber)
            {
                ReportHub.LogWarning(ReportCategory.RESTRICTED_ACTIONS, "OpenExplorerUi: rejected, call did not originate from a recent user gesture");
                return (int)OpenExplorerUiResult.RejectedNoUserGesture;
            }

            if (!TryMapExplorerUi((ExplorerUi)ui, out ExploreSections section, out FeatureId? gatingFeature))
            {
                ReportHub.LogWarning(ReportCategory.RESTRICTED_ACTIONS, $"OpenExplorerUi: unsupported ui value '{ui}'");
                return (int)OpenExplorerUiResult.RejectedFeatureDisabled;
            }

            if (gatingFeature.HasValue && !FeaturesRegistry.Instance.IsEnabled(gatingFeature.Value))
            {
                ReportHub.Log(ReportCategory.RESTRICTED_ACTIONS, $"OpenExplorerUi: feature '{gatingFeature.Value}' is disabled");
                return (int)OpenExplorerUiResult.RejectedFeatureDisabled;
            }

            return (int)explorerUiActions.OpenSection((ExplorerUi)ui, section);
        }

        public void Dispose() { }

        public void TryCopyToClipboard(string text)
        {
            if (!sceneStateProvider.IsCurrent)
                return;

            CopyToClipboardAsync(text).Forget();
        }

        private async UniTask OpenUrlAsync(string url)
        {
            await UniTask.SwitchToMainThread();
            await mvcManager.ShowAsync(ExternalUrlPromptController.IssueCommand(new ExternalUrlPromptController.Params(url)));
        }

        private bool IsPositionValid(Vector3 floorPosition)
        {
            // Avoid teleporting below ground level
            floorPosition.y = Mathf.Max(floorPosition.y, 0);

            var parcelToCheck = floorPosition.ToParcel();

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

        private async UniTask OpenNftDialogAsync(string chain, string contractAddress, string tokenId)
        {
            await UniTask.SwitchToMainThread();
            await mvcManager.ShowAsync(NftPromptController.IssueCommand(new NftPromptController.Params(chain, contractAddress, tokenId)));
        }

        private async UniTask CopyToClipboardAsync(string text)
        {
            await UniTask.SwitchToMainThread();
            systemClipboard.Set(text);
        }

        /// <summary>
        ///     Maps a protocol <see cref="ExplorerUi" /> value to its explore panel section and, when the
        ///     section is behind a synchronous feature flag, the <see cref="FeatureId" /> that gates it.
        ///     Returns false for unknown values so the caller can reject the request.
        /// </summary>
        private static bool TryMapExplorerUi(ExplorerUi ui, out ExploreSections section, out FeatureId? gatingFeature)
        {
            switch (ui)
            {
                case ExplorerUi.EuMap:
                    section = ExploreSections.Navmap;
                    gatingFeature = null;
                    return true;
                case ExplorerUi.EuSettings:
                    section = ExploreSections.Settings;
                    gatingFeature = null;
                    return true;
                case ExplorerUi.EuBackpack:
                    section = ExploreSections.Backpack;
                    gatingFeature = null;
                    return true;
                case ExplorerUi.EuCameraReel:
                    section = ExploreSections.CameraReel;
                    gatingFeature = FeatureId.CameraReel;
                    return true;
                case ExplorerUi.EuCommunities:
                    section = ExploreSections.Communities;
                    // FeatureId.Communities is never populated in FeaturesRegistry (its state is
                    // identity-dependent), so the gate lives in the IExplorerUiActions implementation,
                    // which can reach CommunitiesFeatureAccess.
                    gatingFeature = null;
                    return true;
                case ExplorerUi.EuPlaces:
                    section = ExploreSections.Places;
                    gatingFeature = FeatureId.Discover;
                    return true;
                case ExplorerUi.EuEvents:
                    section = ExploreSections.Events;
                    gatingFeature = FeatureId.Discover;
                    return true;
                default:
                    section = default(ExploreSections);
                    gatingFeature = null;
                    return false;
            }
        }
    }
}
