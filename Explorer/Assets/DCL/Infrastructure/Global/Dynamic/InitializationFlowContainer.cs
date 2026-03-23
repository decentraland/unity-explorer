using DCL.Audio;
using DCL.Character.Plugin;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;

#if !NO_LIVEKIT_MODE
using DCL.Multiplayer.Connections.RoomHubs;
#endif

using DCL.Multiplayer.HealthChecks;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Profiles.Self;
using DCL.RealmNavigation;
using DCL.RealmNavigation.LoadingOperation;
using ECS.SceneLifeCycle.Realm;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.Utilities.Extensions;
using Global;
using Global.AppArgs;
using Global.Dynamic;
using MVC;
using System.Collections.Generic;
using Global.Dynamic;

namespace DCL.UserInAppInitializationFlow
{
    public class InitializationFlowContainer
    {
        public IUserInAppInitializationFlow InitializationFlow { get; private init; } = null!;

        public static InitializationFlowContainer Create(
            StaticContainer staticContainer,
            BootstrapContainer bootstrapContainer,
            RealmContainer realmContainer,
            IRealmNavigator realmNavigator,
            RealmNavigationContainer realmNavigationContainer,
            TerrainContainer terrainContainer,
            ILoadingScreen loadingScreen,
#if !NO_LIVEKIT_MODE
            IHealthCheck liveKitHealthCheck,
#endif
            IMVCManager mvcManager,
            ISelfProfile selfProfile,
            DynamicWorldParams dynamicWorldParams,
            IAppArgs appArgs,
            AudioClipConfig backgroundMusic,

#if !NO_LIVEKIT_MODE
            IRoomHub roomHub,
#endif
#if !UNITY_WEBGL
            bool localSceneDevelopment,
#endif
            CharacterContainer characterContainer)
        {
            ILoadingStatus? loadingStatus = staticContainer.LoadingStatus;

#if !NO_LIVEKIT_MODE
            var ensureLivekitConnectionStartupOperation = new EnsureLivekitConnectionStartupOperation(liveKitHealthCheck, roomHub);
#endif

#if !UNITY_WEBGL
            var blocklistCheckStartupOperation = new BlocklistCheckStartupOperation(staticContainer.WebRequestsContainer.WebRequestController, bootstrapContainer.IdentityCache!, bootstrapContainer.DecentralandUrlsSource);
#endif
            var loadLandscapeStartupOperation = new LoadLandscapeStartupOperation(loadingStatus, terrainContainer.Landscape);
            var teleportStartupOperation = new TeleportStartupOperation(loadingStatus, realmContainer.RealmController, staticContainer.ExposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy, realmContainer.TeleportController, staticContainer.ExposedGlobalDataContainer.CameraSamplingData, dynamicWorldParams.StartParcel);
            var loadPlayerAvatarStartupOperation = new LoadPlayerAvatarStartupOperation(loadingStatus, selfProfile, staticContainer.MainPlayerAvatarBaseProxy);
            var loadingOperations = new List<IStartupOperation>()
            {

#if !UNITY_WEBGL
                blocklistCheckStartupOperation,
#endif
                loadPlayerAvatarStartupOperation,
                loadLandscapeStartupOperation,
                teleportStartupOperation
            };

#if !UNITY_WEBGL
            // The Global PX operation is the 3rd most time-consuming loading stage, and it's currently not needed in Local Scene Development
            // More loading stage measurements for Local Scene Development at https://github.com/decentraland/unity-explorer/pull/3630
            if (!localSceneDevelopment)
            {
                // TODO review why loadGlobalPxOperation is invoked on recovery
                loadingOperations.Add(new LoadGlobalPortableExperiencesStartupOperation(loadingStatus, bootstrapContainer.DebugSettings, staticContainer.PortableExperiencesController));
            }
#endif

#if UNITY_WEBGL
            IAnalyticsController analyticsForOps = IAnalyticsController.Null;
#else
            IAnalyticsController analyticsForOps = bootstrapContainer.Analytics.Controller;
#endif

            var startUpOps = new AnalyticsSequentialLoadingOperation<IStartupOperation.Params>(
                loadingStatus,
                loadingOperations,
                ReportCategory.STARTUP,
                bootstrapContainer.Analytics.Controller,
                "start-up");

            startUpOps.AddDebugControl(realmContainer.DebugView.DebugWidgetBuilder, "Initialization Flow");

            var reLoginOps = new AnalyticsSequentialLoadingOperation<IStartupOperation.Params>(
                loadingStatus,
                loadingOperations,
                ReportCategory.STARTUP,
                bootstrapContainer.Analytics.Controller,
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
#if !NO_LIVEKIT_MODE
                    roomHub,
#endif
                    startUpOps,
                    reLoginOps,
                    bootstrapContainer.IdentityCache.EnsureNotNull(),
#if !NO_LIVEKIT_MODE
                    ensureLivekitConnectionStartupOperation,
#endif
                    appArgs,
                    characterContainer.CharacterObject,
                    characterContainer.Transform,
                    dynamicWorldParams.StartParcel
#if !UNITY_WEBGL
                  , localSceneDevelopment
#endif

                    ),
            };
        }
    }
}










