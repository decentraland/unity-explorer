using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Landscape;
using DCL.LOD;
using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers;
using DCL.Minimap;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.UserInAppInitializationFlow;
using ECS.SceneLifeCycle.Realm;
using System;

namespace Global.Dynamic.TeleportOperations
{
    public class ChangeRealmTeleportOperation : TeleportOperationBase
    {
        private readonly IRealmController realmController;

        private readonly IMapRenderer mapRenderer;
        private readonly RoadAssetsPool roadAssetsPool;
        private readonly SatelliteFloor satelliteFloor;
        private readonly Lazy<MinimapController> minimap;
        private readonly IAnalyticsController analyticsController;

        public ChangeRealmTeleportOperation(
            IMapRenderer mapRenderer,
            RoadAssetsPool roadAssetsPool,
            SatelliteFloor satelliteFloor,
            IRealmController realmController,
            IAnalyticsController analyticsController,
            Lazy<MinimapController> minimap)
        {
            this.mapRenderer = mapRenderer;
            this.roadAssetsPool = roadAssetsPool;
            this.satelliteFloor = satelliteFloor;
            this.analyticsController = analyticsController;
            this.minimap = minimap;
            this.realmController = realmController;
        }

        protected override async UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            float finalizationProgress =
                teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.RealmChanging);

            await realmController.SetRealmAsync(teleportParams.CurrentDestinationRealm, ct);
            SwitchMiscVisibilityAsync();
            teleportParams.ParentReport.SetProgress(finalizationProgress);
        }

        private void SwitchMiscVisibilityAsync()
        {
            var type = realmController.Type;
            bool isGenesis = type is RealmType.GenesisCity;

            minimap.Value!.OnRealmChanged(type);
            analyticsController.Flush();
            mapRenderer.SetSharedLayer(MapLayer.PlayerMarker, isGenesis);
            satelliteFloor.SetCurrentlyInGenesis(isGenesis);
            roadAssetsPool.SwitchVisibility(isGenesis);
        }
    }
}
