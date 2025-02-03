using DCL.Audio;
using DCL.Chat.History;
using DCL.Diagnostics;
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
            IMVCManager mvcManager,
            ISelfProfile selfProfile,
            DynamicWorldParams dynamicWorldParams,
            IAppArgs appArgs,
            AudioClipConfig backgroundMusic,
            IRoomHub roomHub,
            IChatHistory chatHistory)
        {
            ILoadingStatus? loadingStatus = staticContainer.LoadingStatus;

            var ensureLivekitConnectionStartupOperation = new EnsureLivekitConnectionStartupOperation(loadingStatus, liveKitHealthCheck);
            var preloadProfileStartupOperation = new PreloadProfileStartupOperation(loadingStatus, selfProfile);
            var loadPlayerAvatarStartupOperation = new LoadPlayerAvatarStartupOperation(loadingStatus, selfProfile, staticContainer.MainPlayerAvatarBaseProxy);
            var loadLandscapeStartupOperation = new LoadLandscapeStartupOperation(loadingStatus, terrainContainer.Landscape);
            var checkOnboardingStartupOperation = new CheckOnboardingStartupOperation(loadingStatus, selfProfile, staticContainer.FeatureFlagsCache, appArgs, realmNavigationContainer.RealmNavigator);
            var teleportStartupOperation = new TeleportStartupOperation(loadingStatus, realmContainer.RealmController, staticContainer.ExposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy, realmContainer.TeleportController, staticContainer.ExposedGlobalDataContainer.CameraSamplingData, dynamicWorldParams.StartParcel);
            var loadGlobalPxOperation = new LoadGlobalPortableExperiencesStartupOperation(loadingStatus, selfProfile, staticContainer.FeatureFlagsCache, bootstrapContainer.DebugSettings, staticContainer.PortableExperiencesController);
            var sentryDiagnostics = new SentryDiagnosticStartupOperation(realmContainer.RealmController, bootstrapContainer.DiagnosticsContainer);

            var startUpOps = new AnalyticsSequentialLoadingOperation<IStartupOperation.Params>(
                loadingStatus,
                new IStartupOperation[]
                {
                    preloadProfileStartupOperation,
                    loadPlayerAvatarStartupOperation,
                    loadLandscapeStartupOperation,
                    checkOnboardingStartupOperation,
                    teleportStartupOperation,
                    ensureLivekitConnectionStartupOperation, // GateKeeperRoom is dependent on player position so it must be after teleport
                    loadGlobalPxOperation,
                    sentryDiagnostics,
                },
                ReportCategory.STARTUP,
                bootstrapContainer.Analytics.EnsureNotNull(),
                "start-up");

            startUpOps.AddDebugControl(realmContainer.DebugView.DebugWidgetBuilder, "Initialization Flow");

            var reLoginOps = new AnalyticsSequentialLoadingOperation<IStartupOperation.Params>(
                loadingStatus,
                new IStartupOperation[]
                {
                    preloadProfileStartupOperation,
                    loadPlayerAvatarStartupOperation,
                    loadLandscapeStartupOperation,
                    checkOnboardingStartupOperation,
                    teleportStartupOperation,
                    ensureLivekitConnectionStartupOperation,
                    loadGlobalPxOperation,
                    sentryDiagnostics,
                },
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
                    checkOnboardingStartupOperation),
            };
        }
    }
}
