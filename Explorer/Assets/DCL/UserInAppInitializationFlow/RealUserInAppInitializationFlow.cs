using System;
using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Audio;
using DCL.AuthenticationScreenFlow;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Profiles.Self;
using DCL.Utilities;
using MVC;
using System.Threading;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.UserInAppInitializationFlow.StartupOperations;
using DCL.Web3.Identities;
using ECS.SceneLifeCycle.Realm;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.UserInAppInitializationFlow
{
    public class RealUserInAppInitializationFlow : IUserInAppInitializationFlow
    {
        private readonly IMVCManager mvcManager;
        private readonly AudioClipConfig backgroundMusic;
        private readonly IRealmNavigator realmNavigator;
        private readonly ILoadingScreen loadingScreen;
        private readonly IFeatureFlagsProvider featureFlagsProvider;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IRealmController realmController;
        private readonly Dictionary<string, string> appParameters;

        private readonly PreloadProfileStartupOperation preloadProfileStartupOperation;
        private readonly LoadPlayerAvatarStartupOperation loadPlayerAvatarStartupOperation;
        private readonly LoadLandscapeStartupOperation loadLandscapeStartupOperation;
        private readonly TeleportStartupOperation teleportStartupOperation;

        private static readonly ILoadingScreen.EmptyLoadingScreen EMPTY_LOADING_SCREEN = new ();

        public RealUserInAppInitializationFlow(
            RealFlowLoadingStatus loadingStatus,
            IMVCManager mvcManager,
            ISelfProfile selfProfile,
            Vector2Int startParcel,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            AudioClipConfig backgroundMusic,
            IRealmNavigator realmNavigator,
            ILoadingScreen loadingScreen,
            IFeatureFlagsProvider featureFlagsProvider,
            IWeb3IdentityCache web3IdentityCache,
            IRealmController realmController,
            Dictionary<string, string> appParameters)
        {
            this.mvcManager = mvcManager;
            this.backgroundMusic = backgroundMusic;
            this.realmNavigator = realmNavigator;
            this.loadingScreen = loadingScreen;
            this.featureFlagsProvider = featureFlagsProvider;
            this.web3IdentityCache = web3IdentityCache;
            this.realmController = realmController;
            this.appParameters = appParameters;

            preloadProfileStartupOperation = new PreloadProfileStartupOperation(loadingStatus, selfProfile);
            loadPlayerAvatarStartupOperation = new LoadPlayerAvatarStartupOperation(selfProfile, mainPlayerAvatarBaseProxy);
            loadLandscapeStartupOperation = new LoadLandscapeStartupOperation(loadingStatus, realmNavigator);
            teleportStartupOperation = new TeleportStartupOperation(loadingStatus, realmNavigator, startParcel);
        }

        public async UniTask ExecuteAsync(bool showAuthentication,
            bool showLoading,
            bool reloadRealm,
            World world,
            Entity playerEntity,
            CancellationToken ct)
        {
            loadPlayerAvatarStartupOperation.AssignWorld(world, playerEntity);
            using var playAudioScope = UIAudioEventsBus.Instance.NewPlayAudioScope(backgroundMusic);
            if (showAuthentication) await ShowAuthenticationScreenAsync(ct);
            await LoadingScreen(showLoading).ShowWhileExecuteTaskAsync(parentLoadReport => LoadGameAsync(reloadRealm, parentLoadReport, ct), ct);
        }

        private async UniTask LoadGameAsync(bool reloadRealm, AsyncLoadProcessReport parentLoadReport, CancellationToken ct)
        {
            // Re-initialize feature flags since the user might have changed thus the data to be resolved
            await InitializeFeatureFlagsAsync(ct);
            await preloadProfileStartupOperation.ExecuteAsync(parentLoadReport, ct);
            await realmNavigator.SwitchMiscVisibilityAsync();
            await loadPlayerAvatarStartupOperation.ExecuteAsync(parentLoadReport, ct);
            await loadLandscapeStartupOperation.ExecuteAsync(parentLoadReport, ct);
            await TryRestartRealmAsync(reloadRealm, ct);
            await teleportStartupOperation.ExecuteAsync(parentLoadReport, ct);
        }

        private async UniTask TryRestartRealmAsync(bool reloadRealm, CancellationToken ct)
        {
            if (reloadRealm)
                await realmController.RestartRealmAsync(ct);
        }

        private async UniTask ShowAuthenticationScreenAsync(CancellationToken ct)
        {
            await mvcManager.ShowAsync(AuthenticationScreenController.IssueCommand(), ct);
        }

        private async UniTask InitializeFeatureFlagsAsync(CancellationToken ct)
        {
            try { await featureFlagsProvider.InitializeAsync(web3IdentityCache.Identity?.Address, appParameters, ct); }
            catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, new ReportData(ReportCategory.FEATURE_FLAGS)); }
        }

        private ILoadingScreen LoadingScreen(bool withUI) =>
            withUI ? loadingScreen : EMPTY_LOADING_SCREEN;
    }
}
