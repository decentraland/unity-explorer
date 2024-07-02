using System;
using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Audio;
using DCL.AuthenticationScreenFlow;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Utilities;
using MVC;
using System.Threading;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.Web3.Identities;
using ECS.SceneLifeCycle.Realm;
using UnityEngine;
using static DCL.UserInAppInitializationFlow.RealFlowLoadingStatus.Stage;

namespace DCL.UserInAppInitializationFlow
{
    public class RealUserInitializationFlowController : IUserInAppInitializationFlow
    {
        private readonly IMVCManager mvcManager;
        private readonly ISelfProfile selfProfile;
        private readonly Vector2Int startParcel;
        private readonly RealFlowLoadingStatus loadingStatus;
        private readonly ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy;
        private readonly AudioClipConfig backgroundMusic;
        private readonly IRealmNavigator realmNavigator;
        private readonly ILoadingScreen loadingScreen;
        private readonly IFeatureFlagsProvider featureFlagsProvider;
        private readonly IWeb3IdentityCache web3IdentityCache;

        public RealUserInitializationFlowController(RealFlowLoadingStatus loadingStatus,
            IMVCManager mvcManager,
            ISelfProfile selfProfile,
            Vector2Int startParcel,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            AudioClipConfig backgroundMusic,
            IRealmNavigator realmNavigator,
            ILoadingScreen loadingScreen,
            IFeatureFlagsProvider featureFlagsProvider,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.mvcManager = mvcManager;
            this.selfProfile = selfProfile;
            this.startParcel = startParcel;
            this.loadingStatus = loadingStatus;
            this.mainPlayerAvatarBaseProxy = mainPlayerAvatarBaseProxy;
            this.backgroundMusic = backgroundMusic;
            this.realmNavigator = realmNavigator;
            this.loadingScreen = loadingScreen;
            this.featureFlagsProvider = featureFlagsProvider;
            this.web3IdentityCache = web3IdentityCache;
        }

        public async UniTask ExecuteAsync(bool showAuthentication,
            bool showLoading,
            World world,
            Entity playerEntity,
            CancellationToken ct)
        {
            UIAudioEventsBus.Instance.SendPlayContinuousAudioEvent(backgroundMusic);

            if (showAuthentication)
                await ShowAuthenticationScreenAsync(ct);

            // Re-initialize feature flags since the user might have changed thus the data to be resolved
            await InitializeFeatureFlagsAsync(ct);

            if (showLoading)
                await loadingScreen.ShowWhileExecuteTaskAsync(parentLoadReport => LoadCharacterAndWorldAsync(parentLoadReport, world, playerEntity, ct), ct);
            else
                await LoadCharacterAndWorldAsync(AsyncLoadProcessReport.Create(), world, playerEntity, ct);

            UIAudioEventsBus.Instance.SendStopPlayingContinuousAudioEvent(backgroundMusic);
        }

        private async UniTask LoadCharacterAndWorldAsync(AsyncLoadProcessReport parentLoadReport, World world, Entity playerEntity, CancellationToken ct)
        {
            Profile ownProfile = await selfProfile.ProfileOrPublishIfNotAsync(ct);
            parentLoadReport.SetProgress(loadingStatus.SetStage(ProfileLoaded));

            await realmNavigator.SwitchMiscVisibilityAsync();
            await LoadPlayerAvatar(world, playerEntity, ownProfile, ct);

            AsyncLoadProcessReport? landscapeLoadReport
                = parentLoadReport.CreateChildReport(RealFlowLoadingStatus.PROGRESS[LandscapeLoaded]);

            await realmNavigator.LoadTerrainAsync(landscapeLoadReport, ct);
            parentLoadReport.SetProgress(loadingStatus.SetStage(LandscapeLoaded));

            AsyncLoadProcessReport? teleportLoadReport
                = parentLoadReport.CreateChildReport(RealFlowLoadingStatus.PROGRESS[PlayerTeleported]);

            await realmNavigator.InitializeTeleportToSpawnPointAsync(teleportLoadReport, ct, startParcel);
            parentLoadReport.SetProgress(loadingStatus.SetStage(Completed));
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

        private async UniTask ShowAuthenticationScreenAsync(CancellationToken ct)
        {
            await mvcManager.ShowAsync(AuthenticationScreenController.IssueCommand(), ct);
        }

        private async UniTask InitializeFeatureFlagsAsync(CancellationToken ct)
        {
            try
            {
                await featureFlagsProvider.InitializeAsync(web3IdentityCache.Identity?.Address, ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                ReportHub.LogException(e, new ReportData(ReportCategory.FEATURE_FLAGS));
            }
        }
    }
}
