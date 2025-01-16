using DCL.Audio;
using DCL.Chat.History;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.HealthChecks;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.RealmNavigation.LoadingOperation;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.UserInAppInitializationFlow.StartupOperations;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using Global;
using Global.AppArgs;
using Global.Dynamic;
using Global.Dynamic.DebugSettings;
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
            IWeb3IdentityCache identityCache,
            AudioClipConfig backgroundMusic,
            IRoomHub roomHub,
            IChatHistory chatHistory)
        {
            ILoadingStatus? loadingStatus = staticContainer.LoadingStatus;

            var ensureLivekitConnectionStartupOperation = new EnsureLivekitConnectionStartupOperation(loadingStatus, liveKitHealthCheck);
            var initializeFeatureFlagsStartupOperation = new InitializeFeatureFlagsStartupOperation(loadingStatus, staticContainer.FeatureFlagsProvider, identityCache, bootstrapContainer.DecentralandUrlsSource, appArgs);
            var preloadProfileStartupOperation = new PreloadProfileStartupOperation(loadingStatus, selfProfile);
            var loadPlayerAvatarStartupOperation = new LoadPlayerAvatarStartupOperation(loadingStatus, selfProfile, staticContainer.MainPlayerAvatarBaseProxy);
            var loadLandscapeStartupOperation = new LoadLandscapeStartupOperation(loadingStatus, terrainContainer.Landscape);
            var checkOnboardingStartupOperation = new CheckOnboardingStartupOperation(loadingStatus, selfProfile, staticContainer.FeatureFlagsCache, appArgs, realmNavigationContainer.RealmNavigator);
            var teleportStartupOperation = new TeleportStartupOperation(loadingStatus, realmContainer.RealmController, staticContainer.ExposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy, realmContainer.TeleportController, staticContainer.ExposedGlobalDataContainer.CameraSamplingData, dynamicWorldParams.StartParcel);
            var loadGlobalPxOperation = new LoadGlobalPortableExperiencesStartupOperation(loadingStatus, selfProfile, staticContainer.FeatureFlagsCache, bootstrapContainer.DebugSettings, staticContainer.PortableExperiencesController);
            var sentryDiagnostics = new SentryDiagnosticStartupOperation(realmContainer.RealmController, bootstrapContainer.DiagnosticsContainer);

            var startUpOps = new AnalyticsStartupOperation(
                bootstrapContainer.Analytics.EnsureNotNull(),
                loadingStatus,
                new IStartupOperation[]
                {
                    ensureLivekitConnectionStartupOperation,
                    initializeFeatureFlagsStartupOperation,
                    preloadProfileStartupOperation,
                    loadPlayerAvatarStartupOperation,
                    loadLandscapeStartupOperation,
                    checkOnboardingStartupOperation,
                    teleportStartupOperation,
                    loadGlobalPxOperation,
                    sentryDiagnostics,
                },
                ReportCategory.STARTUP);

            startUpOps.AddDebugControl(realmContainer.DebugView.DebugWidgetBuilder, "Initialization Flow");

            var reLoginOps = new AnalyticsStartupOperation(
                bootstrapContainer.Analytics.EnsureNotNull(),
                loadingStatus,
                new IStartupOperation[]
                {
                    ensureLivekitConnectionStartupOperation,
                    preloadProfileStartupOperation,
                    loadPlayerAvatarStartupOperation,
                    loadLandscapeStartupOperation,
                    checkOnboardingStartupOperation,
                    teleportStartupOperation,
                    loadGlobalPxOperation,
                    sentryDiagnostics,
                }, ReportCategory.STARTUP);

            startUpOps.AddDebugControl(realmContainer.DebugView.DebugWidgetBuilder, "Re-Login Flow");

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
