using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.AuthenticationScreenFlow;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Landscape.Interface;
using DCL.ParcelsService;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.SceneLoadingScreens;
using DCL.Utilities;
using ECS.Prioritization.Components;
using ECS.SceneLifeCycle.Reporting;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.Assertions;
using static DCL.UserInAppInitializationFlow.RealFlowLoadingStatus.Stage;

namespace DCL.UserInAppInitializationFlow
{
    public class RealUserInitializationFlowController : IUserInAppInitializationFlow
    {
        private readonly ITeleportController teleportController;
        private readonly IMVCManager mvcManager;
        private readonly ISelfProfile selfProfile;
        private readonly Vector2Int startParcel;
        private readonly bool enableLandscape;
        private readonly ILandscapeInitialization landscapeInitialization;
        private readonly RealFlowLoadingStatus loadingStatus;

        private readonly CameraSamplingData cameraSamplingData;
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private readonly ObjectProxy<Entity> cameraEntity;

        private AsyncLoadProcessReport? loadReport;

        public RealUserInitializationFlowController(RealFlowLoadingStatus loadingStatus,
            ITeleportController teleportController,
            IMVCManager mvcManager,
            ISelfProfile selfProfile,
            Vector2Int startParcel,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            ObjectProxy<Entity> cameraEntity,
            CameraSamplingData cameraSamplingData,
            bool enableLandscape,
            ILandscapeInitialization landscapeInitialization
        )
        {
            this.teleportController = teleportController;
            this.mvcManager = mvcManager;
            this.selfProfile = selfProfile;
            this.startParcel = startParcel;
            this.enableLandscape = enableLandscape;
            this.landscapeInitialization = landscapeInitialization;
            this.loadingStatus = loadingStatus;
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
            this.cameraEntity = cameraEntity;
            this.cameraSamplingData = cameraSamplingData;
        }

        public async UniTask ExecuteAsync(bool showAuthentication,
            bool showLoading,
            World world,
            Entity playerEntity,
            CancellationToken ct)
        {
            loadReport = AsyncLoadProcessReport.Create();

            if (showAuthentication)
                await ShowAuthenticationScreenAsync(ct);

            UniTask showLoadingScreenAsyncTask = LoadCharacterAndWorldAsync(world, playerEntity, ct);

            if (showLoading)
                await UniTask.WhenAll(ShowLoadingScreenAsync(ct), showLoadingScreenAsyncTask);
            else
                await showLoadingScreenAsyncTask;
        }

        private async UniTask LoadCharacterAndWorldAsync(World world, Entity ownPlayerEntity, CancellationToken ct)
        {
            Profile ownProfile = await selfProfile.ProfileOrPublishIfNotAsync(ct);

            loadReport!.ProgressCounter.Value = loadingStatus.SetStage(ProfileLoaded);

            await LoadPlayerAvatar(world, ownPlayerEntity, ownProfile, ct);

            await LoadLandscapeAsync(ct);

            await TeleportToSpawnPointAsync(world, ct);

            loadReport.ProgressCounter.Value = loadingStatus.SetStage(Completed);
            loadReport.CompletionSource.TrySetResult();
        }

        private async UniTask LoadLandscapeAsync(CancellationToken ct)
        {
            if (enableLandscape)
            {
                var landscapeLoadReport = new AsyncLoadProcessReport(new UniTaskCompletionSource(), new AsyncReactiveProperty<float>(0));

                await UniTask.WhenAny(
                    landscapeLoadReport.PropagateProgressCounterAsync(loadReport, ct, loadReport!.ProgressCounter.Value, RealFlowLoadingStatus.PROGRESS[LandscapeLoaded]),
                    landscapeInitialization.InitializeLoadingProgressAsync(landscapeLoadReport, ct));
            }

            loadReport!.ProgressCounter.Value = loadingStatus.SetStage(LandscapeLoaded);
        }

        /// <summary>
        ///     Resolves Player profile and waits for the avatar to be loaded
        /// </summary>
        private UniTask LoadPlayerAvatar(World world, Entity playerEntity, Profile profile, CancellationToken ct)
        {
            // Add the profile into the player entity so it will create the avatar in world

            if (world.Has<Profile>(playerEntity))
                world.Set(playerEntity, profile);
            else
                world.Add(playerEntity, profile);

            // Eventually it will lead to the Avatar Resolution or the entity destruction
            // if the avatar is already downloaded by the authentication screen it will be resolved immediately
            return UniTask.WaitWhile(() => !mainPlayerAvatarBaseProxy.Configured && world.IsAlive(playerEntity), PlayerLoopTiming.LastPostLateUpdate, ct);
        }

        private async UniTask TeleportToSpawnPointAsync(World world, CancellationToken ct)
        {
            var teleportLoadReport = new AsyncLoadProcessReport(new UniTaskCompletionSource(), new AsyncReactiveProperty<float>(0));

            await UniTask.WhenAny(teleportLoadReport.PropagateProgressCounterAsync(loadReport, ct, loadReport!.ProgressCounter.Value, RealFlowLoadingStatus.PROGRESS[PlayerTeleported]),
                WaitForTeleportAndStartPartitioningAsync());

            loadingStatus.SetStage(PlayerTeleported);

            async UniTask WaitForTeleportAndStartPartitioningAsync()
            {
                var waitForSceneReadiness = await teleportController.TeleportToSceneSpawnPointAsync(startParcel, teleportLoadReport, ct);

                // add camera sampling data to the camera entity to start partitioning
                Assert.IsTrue(cameraEntity.Configured);
                world.Add(cameraEntity.Object, cameraSamplingData);

                // Wait for the scene to fire scene readiness
                await waitForSceneReadiness.ToUniTask(ct);
            }
        }

        private async UniTask ShowLoadingScreenAsync(CancellationToken ct)
        {
            var timeout = TimeSpan.FromMinutes(2);

            await mvcManager.ShowAsync(SceneLoadingScreenController.IssueCommand(new SceneLoadingScreenController.Params(loadReport!, timeout)), ct);
        }

        private async UniTask ShowAuthenticationScreenAsync(CancellationToken ct)
        {
            await mvcManager.ShowAsync(AuthenticationScreenController.IssueCommand(), ct);
        }
    }
}
