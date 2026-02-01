using Arch.Core;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.LOD.Systems;
#if !NO_LIVEKIT_MODE
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Entities;
#endif
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.RealmNavigation.LoadingOperation;
using DCL.RealmNavigation.TeleportOperations;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.Utilities.Extensions;
using ECS.SceneLifeCycle.Realm;
using Global;
using Global.Dynamic;
using System;

namespace DCL.RealmNavigation
{
    public class RealmNavigationContainer
    {
        private static readonly TimeSpan LIVEKIT_TIMEOUT = TimeSpan.FromSeconds(10f);

        /// <summary>
        ///     Realm Navigator with core teleport functionality
        /// </summary>
        public IRealmNavigator RealmNavigator { get; private init; } = null!;

        private DebugWidgetBuilder? widgetBuilder { get; init; }

        public RealmNavigationDebugPlugin CreatePlugin() =>
            new (widgetBuilder);

        public static RealmNavigationContainer Create(
            StaticContainer staticContainer,
            BootstrapContainer bootstrapContainer,
            LODContainer lodContainer,
            RealmContainer realmContainer,

#if !NO_LIVEKIT_MODE
            RemoteEntities remoteEntities,
#endif

            World globalWorld,

#if !NO_LIVEKIT_MODE
            IRoomHub roomHub,
#endif

#if !UNITY_WEBGL
            ILandscape landscape,
#endif

            ExposedGlobalDataContainer exposedGlobalDataContainer,
            ILoadingScreen loadingScreen)
        {
            const string ANALYTICS_OP_NAME = "teleportation";

            IAnalyticsController analytics = bootstrapContainer.Analytics.EnsureNotNull();

            var realmChangeOperations = new AnalyticsSequentialLoadingOperation<TeleportParams>(staticContainer.LoadingStatus, new ITeleportOperation[]
            {
                new RestartLoadingStatus(),

#if !NO_LIVEKIT_MODE
                new RemoveRemoteEntitiesTeleportOperation(remoteEntities, globalWorld),
                new StopRoomAsyncTeleportOperation(roomHub, LIVEKIT_TIMEOUT),
#endif

                new RemoveCameraSamplingDataTeleportOperation(globalWorld, exposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy),
                new ChangeRealmTeleportOperation(realmContainer.RealmController),
                new AnalyticsFlushTeleportOperation(analytics),

#if !UNITY_WEBGL
                new LoadLandscapeTeleportOperation(landscape),
#endif

                new PrewarmRoadAssetPoolsTeleportOperation(realmContainer.RealmController, lodContainer.RoadAssetsPool),
                new UnloadCacheImmediateTeleportOperation(staticContainer.CacheCleaner, staticContainer.SingletonSharedDependencies.MemoryBudget),
                new MoveToParcelInNewRealmTeleportOperation(staticContainer.LoadingStatus, realmContainer.RealmController, exposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy, realmContainer.TeleportController, exposedGlobalDataContainer.CameraSamplingData),

#if !NO_LIVEKIT_MODE
                new RestartRoomAsyncTeleportOperation(roomHub, LIVEKIT_TIMEOUT),
#endif

            },
            ReportCategory.SCENE_LOADING,
            analytics,
            ANALYTICS_OP_NAME);

            var teleportInSameRealmOperation = new AnalyticsSequentialLoadingOperation<TeleportParams>(staticContainer.LoadingStatus,
                new ITeleportOperation[]
                {
                    new RestartLoadingStatus(),
                    new UnloadCacheImmediateTeleportOperation(staticContainer.CacheCleaner, staticContainer.SingletonSharedDependencies.MemoryBudget),
                    new MoveToParcelInSameRealmTeleportOperation(realmContainer.TeleportController),
                }, ReportCategory.SCENE_LOADING,
                analytics,
                ANALYTICS_OP_NAME);

            realmChangeOperations.AddDebugControl(realmContainer.DebugView.DebugWidgetBuilder, "Realm Change");
            teleportInSameRealmOperation.AddDebugControl(realmContainer.DebugView.DebugWidgetBuilder, "Teleport In Same Realm");

            return new RealmNavigationContainer
            {
                RealmNavigator = new RealmNavigator(
                        loadingScreen, 
                        realmContainer.RealmController, 
                        bootstrapContainer.DecentralandUrlsSource, 
                        globalWorld, 
                        exposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy, 
                        exposedGlobalDataContainer.CameraSamplingData, 
                        staticContainer.LoadingStatus, 

#if !UNITY_WEBGL
                        landscape, 
#endif

                        bootstrapContainer.Analytics!,
                        realmChangeOperations, 
                        teleportInSameRealmOperation
                        ),
                widgetBuilder = realmContainer.DebugView.DebugWidgetBuilder,
            };
        }
    }
}
