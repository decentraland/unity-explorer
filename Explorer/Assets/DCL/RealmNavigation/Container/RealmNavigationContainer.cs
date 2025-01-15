using Arch.Core;
using DCL.Diagnostics;
using DCL.LOD.Systems;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Entities;
using DCL.ParcelsService;
using DCL.RealmNavigation.LoadingOperation;
using DCL.RealmNavigation.TeleportOperations;
using DCL.SceneLoadingScreens.LoadingScreen;
using ECS.SceneLifeCycle.Realm;
using Global;
using Global.Dynamic;
using System;

namespace DCL.RealmNavigation
{
    public class RealmNavigationContainer
    {
        private static readonly TimeSpan LIVEKIT_TIMEOUT = TimeSpan.FromSeconds(10f);

        public IRealmNavigator RealmNavigator { get; private set; }

        public static RealmNavigationContainer Create(
            StaticContainer staticContainer,
            BootstrapContainer bootstrapContainer,
            LODContainer lodContainer,
            RealmContainer realmContainer,
            RemoteEntities remoteEntities,
            World globalWorld,
            IRoomHub roomHub,
            IRealmMisc realmMisc,
            ILandscape landscape,
            ExposedGlobalDataContainer exposedGlobalDataContainer,
            ILoadingScreen loadingScreen)
        {
            // TODO build operations debug widget

            var realmChangeOperations = new SequentialLoadingOperation<TeleportParams>(staticContainer.LoadingStatus, new ITeleportOperation[]
            {
                new RestartLoadingStatus(),
                new RemoveRemoteEntitiesTeleportOperation(remoteEntities, globalWorld),
                new StopRoomAsyncTeleportOperation(roomHub, LIVEKIT_TIMEOUT),
                new RemoveCameraSamplingDataTeleportOperation(globalWorld, exposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy),
                new DestroyAllRoadAssetsTeleportOperation(globalWorld, lodContainer.RoadAssetsPool),
                new ChangeRealmTeleportOperation(realmContainer.RealmController, realmMisc),
                new AnalyticsFlushTeleportOperation(bootstrapContainer.Analytics),
                new LoadLandscapeTeleportOperation(landscape),
                new PrewarmRoadAssetPoolsTeleportOperation(realmContainer.RealmController, lodContainer.RoadAssetsPool),
                new UnloadCacheImmediateTeleportOperation(staticContainer.CacheCleaner, staticContainer.SingletonSharedDependencies.MemoryBudget),
                new MoveToParcelInNewRealmTeleportOperation(staticContainer.LoadingStatus, realmContainer.RealmController, exposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy, realmContainer.TeleportController, exposedGlobalDataContainer.CameraSamplingData),
                new RestartRoomAsyncTeleportOperation(roomHub, LIVEKIT_TIMEOUT),
            }, ReportCategory.SCENE_LOADING);

            var teleportInSameRealmOperation = new SequentialLoadingOperation<TeleportParams>(staticContainer.LoadingStatus,
                new ITeleportOperation[]
                {
                    new RestartLoadingStatus(),
                    new UnloadCacheImmediateTeleportOperation(staticContainer.CacheCleaner, staticContainer.SingletonSharedDependencies.MemoryBudget),
                    new MoveToParcelInSameRealmTeleportOperation(realmContainer.TeleportController),
                }, ReportCategory.SCENE_LOADING);

            return new RealmNavigationContainer
            {
                RealmNavigator = new RealmNavigator(loadingScreen, realmContainer.RealmController, bootstrapContainer.DecentralandUrlsSource, globalWorld, exposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy, exposedGlobalDataContainer.CameraSamplingData, staticContainer.LoadingStatus, landscape, bootstrapContainer.Analytics!,
                    realmChangeOperations, teleportInSameRealmOperation),
            };
        }
    }
}
