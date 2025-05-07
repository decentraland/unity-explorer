using DCL.Audio;
using DCL.Chat.History;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.HealthChecks;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.RealmNavigation.LoadingOperation;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.UserInAppInitializationFlow.StartupOperations;
using DCL.Utilities.Extensions;
using Global;
using Global.AppArgs;
using Global.Dynamic;
using MVC;
using System.Collections.Generic;

namespace DCL.UserInAppInitializationFlow
{
    public class InitializationFlowContainer
    {
        public IUserInAppInitializationFlow InitializationFlow { get; private init; } = null!;

        public static InitializationFlowContainer Create(
            StaticContainer staticContainer,
            BootstrapContainer bootstrapContainer,
            RealmContainer realmContainer,
            RealmNavigationContainer realmNavigationContainer,
            TerrainContainer terrainContainer,
            ILoadingScreen loadingScreen,
            IHealthCheck liveKitHealthCheck,
            IDecentralandUrlsSource decentralandUrlsSource,
            IMVCManager mvcManager,
            ISelfProfile selfProfile,
            DynamicWorldParams dynamicWorldParams,
            IAppArgs appArgs,
            AudioClipConfig backgroundMusic,
            IRoomHub roomHub,
            IChatHistory chatHistory,
            bool localSceneDevelopment)
        {
            ILoadingStatus? loadingStatus = staticContainer.LoadingStatus;

            var ensureLivekitConnectionStartupOperation = new EnsureLivekitConnectionStartupOperation(loadingStatus, liveKitHealthCheck);
            var preloadProfileStartupOperation = new PreloadProfileStartupOperation(loadingStatus, selfProfile);
            var blocklistCheckStartupOperation = new BlocklistCheckStartupOperation(staticContainer.WebRequestsContainer, bootstrapContainer.IdentityCache!, bootstrapContainer.DecentralandUrlsSource);
            var loadPlayerAvatarStartupOperation = new LoadPlayerAvatarStartupOperation(loadingStatus, selfProfile, staticContainer.MainPlayerAvatarBaseProxy);
            var loadLandscapeStartupOperation = new LoadLandscapeStartupOperation(loadingStatus, terrainContainer.Landscape);
            var checkOnboardingStartupOperation = new CheckOnboardingStartupOperation(loadingStatus, selfProfile, staticContainer.FeatureFlagsCache, decentralandUrlsSource, appArgs, realmNavigationContainer.RealmNavigator);
            var teleportStartupOperation = new TeleportStartupOperation(loadingStatus, realmContainer.RealmController, staticContainer.ExposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy, realmContainer.TeleportController, staticContainer.ExposedGlobalDataContainer.CameraSamplingData, dynamicWorldParams.StartParcel);

            var sentryDiagnostics = new SentryDiagnosticStartupOperation(realmContainer.RealmController, bootstrapContainer.DiagnosticsContainer);

            var loadingOperations = new List<IStartupOperation>()
            {
                blocklistCheckStartupOperation,
                preloadProfileStartupOperation,
                loadPlayerAvatarStartupOperation,
                loadLandscapeStartupOperation,
                checkOnboardingStartupOperation,
                teleportStartupOperation,
                ensureLivekitConnectionStartupOperation, // GateKeeperRoom is dependent on player position so it must be after teleport
                sentryDiagnostics
            };

            // The Global PX operation is the 3rd most time-consuming loading stage and it's currently not needed in Local Scene Development
            // More loading stage measurements for Local Scene Development at https://github.com/decentraland/unity-explorer/pull/3630
            if (!localSceneDevelopment)
            {
                // TODO review why loadGlobalPxOperation is invoked on recovery
                loadingOperations.Add(new LoadGlobalPortableExperiencesStartupOperation(loadingStatus, staticContainer.FeatureFlagsCache, bootstrapContainer.DebugSettings, staticContainer.PortableExperiencesController));
            }

            var startUpOps = new AnalyticsSequentialLoadingOperation<IStartupOperation.Params>(
                loadingStatus,
                loadingOperations,
                ReportCategory.STARTUP,
                bootstrapContainer.Analytics.EnsureNotNull(),
                "start-up");

            startUpOps.AddDebugControl(realmContainer.DebugView.DebugWidgetBuilder, "Initialization Flow");

            var reLoginOps = new AnalyticsSequentialLoadingOperation<IStartupOperation.Params>(
                loadingStatus,
                loadingOperations,
                ReportCategory.STARTUP,
                bootstrapContainer.Analytics.EnsureNotNull(),
                "re-login");

            reLoginOps.AddDebugControl(realmContainer.DebugView.DebugWidgetBuilder, "Re-Login Flow");

            return new InitializationFlowContainer
            {
                InitializationFlow = new RealUserInAppInitializationFlow(loadingStatus,
                    bootstrapContainer.DecentralandUrlsSource,
                    mvcManager,
                    backgroundMusic,
                    realmNavigationContainer.RealmNavigator,
                    loadingScreen,
                    realmContainer.RealmController,
                    staticContainer.PortableExperiencesController,
                    roomHub,
                    chatHistory,
                    startUpOps,
                    reLoginOps,
                    checkOnboardingStartupOperation,
                    bootstrapContainer.IdentityCache.EnsureNotNull(),
                    appArgs),
            };
        }
    }
}
