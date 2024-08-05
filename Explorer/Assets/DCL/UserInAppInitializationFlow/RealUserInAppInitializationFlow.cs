using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Audio;
using DCL.AuthenticationScreenFlow;
using DCL.AvatarRendering.AvatarShape.UnityInterface;
using DCL.Profiles.Self;
using DCL.Utilities;
using MVC;
using System.Threading;
using DCL.FeatureFlags;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.HealthChecks;
using DCL.Multiplayer.HealthChecks.Livekit;
using DCL.Multiplayer.HealthChecks.Struct;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.UserInAppInitializationFlow.StartupOperations;
using DCL.UserInAppInitializationFlow.StartupOperations.Struct;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS.SceneLifeCycle.Realm;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.UserInAppInitializationFlow
{
    public class RealUserInAppInitializationFlow : IUserInAppInitializationFlow
    {
        private readonly IMVCManager mvcManager;
        private readonly AudioClipConfig backgroundMusic;
        private readonly ILoadingScreen loadingScreen;

        private readonly LoadPlayerAvatarStartupOperation loadPlayerAvatarStartupOperation;
        private readonly RestartRealmStartupOperation restartRealmStartupOperation;

        private readonly IStartupOperation startupOperation;

        private static readonly ILoadingScreen.EmptyLoadingScreen EMPTY_LOADING_SCREEN = new ();

        public RealUserInAppInitializationFlow(
            RealFlowLoadingStatus loadingStatus,
            IRoomHub roomHub,
            IDecentralandUrlsSource decentralandUrlsSource,
            IMVCManager mvcManager,
            ISelfProfile selfProfile,
            IAnalyticsController analyticsController,
            bool debugNoLivekitConnection,
            Vector2Int startParcel,
            ObjectProxy<AvatarBase> mainPlayerAvatarBaseProxy,
            AudioClipConfig backgroundMusic,
            IRealmNavigator realmNavigator,
            ILoadingScreen loadingScreen,
            IFeatureFlagsProvider featureFlagsProvider,
            IWeb3IdentityCache web3IdentityCache,
            IWebRequestController webRequestController,
            IRealmController realmController,
            Dictionary<string, string> appParameters)
        {
            this.mvcManager = mvcManager;
            this.backgroundMusic = backgroundMusic;
            this.loadingScreen = loadingScreen;

            IHealthCheck livekitHealthCheck = debugNoLivekitConnection
                ? new IHealthCheck.AlwaysFails("Livekit connection is in debug, always fail mode")
                : new SeveralHealthCheck(
                      new MultipleURLHealthCheck(webRequestController, decentralandUrlsSource,
                          DecentralandUrl.ArchipelagoStatus,
                          DecentralandUrl.GatekeeperStatus
                      ),
                      new LivekitHealthCheck(roomHub)
                  )
                 //.WithAnalytics(analyticsController, "livekit_health_check_failed")
                 .WithRetries(3);

            var ensureLivekitConnectionStartupOperation = new EnsureLivekitConnectionStartupOperation(livekitHealthCheck);
            var initializeFeatureFlagsStartupOperation = new InitializeFeatureFlagsStartupOperation(featureFlagsProvider, web3IdentityCache, decentralandUrlsSource, appParameters);
            var preloadProfileStartupOperation = new PreloadProfileStartupOperation(loadingStatus, selfProfile);
            var switchRealmMiscVisibilityStartupOperation = new SwitchRealmMiscVisibilityStartupOperation(realmNavigator);
            loadPlayerAvatarStartupOperation = new LoadPlayerAvatarStartupOperation(selfProfile, mainPlayerAvatarBaseProxy);
            var loadLandscapeStartupOperation = new LoadLandscapeStartupOperation(loadingStatus, realmNavigator);
            restartRealmStartupOperation = new RestartRealmStartupOperation(realmController);
            var teleportStartupOperation = new TeleportStartupOperation(loadingStatus, realmNavigator, startParcel);

            startupOperation = new SeveralStartupOperation(
                loadingStatus,
                ensureLivekitConnectionStartupOperation,
                initializeFeatureFlagsStartupOperation,
                preloadProfileStartupOperation,
                switchRealmMiscVisibilityStartupOperation,
                loadPlayerAvatarStartupOperation,
                loadLandscapeStartupOperation,
                restartRealmStartupOperation,
                teleportStartupOperation
            );
        }

        public async UniTask ExecuteAsync(bool showAuthentication,
            bool showLoading,
            bool reloadRealm,
            World world,
            Entity playerEntity,
            CancellationToken ct)
        {
            StartupResult result = default;

            loadPlayerAvatarStartupOperation.AssignWorld(world, playerEntity);
            restartRealmStartupOperation.EnableReload(reloadRealm);

            using var playAudioScope = UIAudioEventsBus.Instance.NewPlayAudioScope(backgroundMusic);

            do
            {
                if (showAuthentication) await ShowAuthenticationScreenAsync(ct);

                await LoadingScreen(showLoading)
                   .ShowWhileExecuteTaskAsync(
                        async parentLoadReport => result = await startupOperation.ExecuteAsync(parentLoadReport, ct),
                        ct
                    );

                //TODO notification popup on failure
            }
            while (result.Success == false && showAuthentication);
        }

        private async UniTask ShowAuthenticationScreenAsync(CancellationToken ct)
        {
            await mvcManager.ShowAsync(AuthenticationScreenController.IssueCommand(), ct);
        }

        private ILoadingScreen LoadingScreen(bool withUI) =>
            withUI ? loadingScreen : EMPTY_LOADING_SCREEN;
    }
}
