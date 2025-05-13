using Arch.Core;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.LOD.Systems;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Entities;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using DCL.RealmNavigation.LoadingOperation;
using DCL.RealmNavigation.TeleportOperations;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.UserInAppInitializationFlow;
using DCL.Utilities.Extensions;
using ECS.LifeCycle;
using ECS.SceneLifeCycle.Realm;
using Global;
using Global.Dynamic;
using System;
using System.Collections.Generic;

namespace DCL.RealmNavigation
{
    public class RealmNavigationContainer
    {
        private static readonly TimeSpan LIVEKIT_TIMEOUT = TimeSpan.FromSeconds(10f);

        private MainScreenFallbackRealmNavigator? mainScreenFallbackRealmNavigator;

        /// <summary>
        ///     Realm Navigator without main-screen fallback functionality
        /// </summary>
        public IRealmNavigator RealmNavigator { get; private init; } = null!;

        private DebugWidgetBuilder? widgetBuilder { get; init; }

        public IRealmNavigator WithMainScreenFallback(IUserInAppInitializationFlow userInAppInitializationFlow, Entity playerEntity, World globalWorld)
        {
            return mainScreenFallbackRealmNavigator ??= new MainScreenFallbackRealmNavigator(RealmNavigator, userInAppInitializationFlow, playerEntity, globalWorld);
        }

        public RealmNavigationDebugPlugin CreatePlugin() =>
            new (widgetBuilder);

        public static RealmNavigationContainer Create(
            StaticContainer staticContainer,
            BootstrapContainer bootstrapContainer,
            LODContainer lodContainer,
            RealmContainer realmContainer,
            RemoteEntities remoteEntities,
            World globalWorld,
            IRoomHub roomHub,
            ILandscape landscape,
            ExposedGlobalDataContainer exposedGlobalDataContainer,
            ILoadingScreen loadingScreen)
        {
            const string ANALYTICS_OP_NAME = "teleportation";

            IAnalyticsController analytics = bootstrapContainer.Analytics.EnsureNotNull();

            var realmChangeOperations = new AnalyticsSequentialLoadingOperation<TeleportParams>(staticContainer.LoadingStatus, new ITeleportOperation[]
            {
                new RestartLoadingStatus(),
                new RemoveRemoteEntitiesTeleportOperation(remoteEntities, globalWorld),
                new StopRoomAsyncTeleportOperation(roomHub, LIVEKIT_TIMEOUT),
                new RemoveCameraSamplingDataTeleportOperation(globalWorld, exposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy),
                new ChangeRealmTeleportOperation(realmContainer.RealmController),
                new AnalyticsFlushTeleportOperation(analytics),
                new LoadLandscapeTeleportOperation(landscape),
                new PrewarmRoadAssetPoolsTeleportOperation(realmContainer.RealmController, lodContainer.RoadAssetsPool),
                new UnloadCacheImmediateTeleportOperation(staticContainer.CacheCleaner, staticContainer.SingletonSharedDependencies.MemoryBudget),
                new MoveToParcelInNewRealmTeleportOperation(staticContainer.LoadingStatus, realmContainer.RealmController, exposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy, realmContainer.TeleportController, exposedGlobalDataContainer.CameraSamplingData),
                new RestartRoomAsyncTeleportOperation(roomHub, LIVEKIT_TIMEOUT),
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
                RealmNavigator = new RealmNavigator(loadingScreen, realmContainer.RealmController, bootstrapContainer.DecentralandUrlsSource, globalWorld, exposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy, exposedGlobalDataContainer.CameraSamplingData, staticContainer.LoadingStatus, landscape, bootstrapContainer.Analytics!,
                    realmChangeOperations, teleportInSameRealmOperation),
                widgetBuilder = realmContainer.DebugView.DebugWidgetBuilder,
            };
        }
    }
}
