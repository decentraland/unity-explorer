using Arch.Core;
using DCL.DebugUtilities;
using DCL.Diagnostics;
using DCL.LOD.Systems;
using DCL.Multiplayer.Connections.RoomHubs;
using DCL.Multiplayer.Profiles.Announcements;
using DCL.Multiplayer.Profiles.Entities;
using DCL.Multiplayer.Profiles.RemoteProfiles;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PlacesAPIService;
using DCL.PrivateWorlds;
using DCL.RealmNavigation.LoadingOperation;
using DCL.RealmNavigation.TeleportOperations;
using DCL.SceneLoadingScreens.LoadingScreen;
using DCL.Web3.Identities;
using ECS.SceneLifeCycle.Realm;
using Global;
using Global.Dynamic;
using MVC;
using SceneRunner.Debugging.Hub;
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

        public IWorldAccessGate WorldAccessGate { get; private init; } = null!;

        public IWorldPermissionsService WorldPermissionsService { get; private init; } = null!;

        public IWorldInfoHub WorldInfoHub { get; private init; } = null!;

        private DebugWidgetBuilder? widgetBuilder { get; init; }

        public RealmNavigationDebugPlugin CreatePlugin() =>
            new (widgetBuilder);

        public static RealmNavigationContainer Create(
            StaticContainer staticContainer,
            BootstrapContainer bootstrapContainer,
            LODContainer lodContainer,
            RealmContainer realmContainer,
            RemoteEntities remoteEntities,
            RemoteProfiles remoteProfiles,
            IRemoteAnnouncements remoteAnnouncements,
            World globalWorld,
            IRoomHub roomHub,
            ILandscape landscape,
            ExposedGlobalDataContainer exposedGlobalDataContainer,
            ILoadingScreen loadingScreen,
            IPlacesAPIService placesAPIService,
            IWeb3IdentityCache identityCache,
            ICommunityMembershipChecker communityMembershipChecker,
            IMVCManager mvcManager)
        {
            const string ANALYTICS_OP_NAME = "teleportation";

            IAnalyticsController analytics = bootstrapContainer.Analytics.Controller;

            var worldPermissionsService = new WorldPermissionsService(staticContainer.WebRequestsContainer.WebRequestController,
                bootstrapContainer.DecentralandUrlsSource, identityCache, communityMembershipChecker);

            var worldAccessGate = new PrivateWorldAccessHandler(worldPermissionsService, mvcManager, staticContainer.RealmData);

            var worldInfoHub = new LocationBasedWorldInfoHub(
                new WorldInfoHub(staticContainer.SingletonSharedDependencies.SceneMapping),
                staticContainer.CharacterContainer.CharacterObject);

            var realmChangeOperations = new AnalyticsSequentialLoadingOperation<TeleportParams>(staticContainer.LoadingStatus, new ITeleportOperation[]
                {
                    new RestartLoadingStatus(),
                    new RemoveRemoteEntitiesTeleportOperation(remoteEntities, globalWorld),
                    new StopRoomAsyncTeleportOperation(roomHub, LIVEKIT_TIMEOUT),
                    new FlushRemoteProfilesTeleportOperation(remoteProfiles, remoteAnnouncements),
                    new RemoveCameraSamplingDataTeleportOperation(globalWorld, exposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy),
                    new ClearWorldsCacheTeleportOperation(placesAPIService),
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
                RealmNavigator = new RealmNavigator(
                    loadingScreen,
                    realmContainer.RealmController,
                    bootstrapContainer.DecentralandUrlsSource,
                    globalWorld,
                    exposedGlobalDataContainer.ExposedCameraData.CameraEntityProxy,
                    exposedGlobalDataContainer.CameraSamplingData,
                    staticContainer.LoadingStatus,
                    landscape,
                    analytics,
                    realmChangeOperations,
                    teleportInSameRealmOperation,
                    worldAccessGate),
                WorldAccessGate = worldAccessGate,
                WorldPermissionsService = worldPermissionsService,
                WorldInfoHub = worldInfoHub,
                widgetBuilder = realmContainer.DebugView.DebugWidgetBuilder
            };
        }
    }
}
