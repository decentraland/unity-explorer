using System.Threading;
using Cysharp.Threading.Tasks;
using DCL.Landscape;
using DCL.LOD;
using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers;
using DCL.UserInAppInitializationFlow;
using ECS.SceneLifeCycle.Realm;


namespace Global.Dynamic.TeleportOperations
{
    public class ChangeRealmTeleportOperation : TeleportOperationBase
    {
        private readonly RealmNavigator realmNavigator;
        private readonly IRealmController realmController;


        private readonly IMapRenderer mapRenderer;
        private readonly RoadAssetsPool roadAssetsPool;
        private readonly SatelliteFloor satelliteFloor;

        public ChangeRealmTeleportOperation(RealmNavigator realmNavigator, IRealmController realmController, IMapRenderer mapRenderer, RoadAssetsPool roadAssetsPool, SatelliteFloor satelliteFloor)
        {
            this.realmNavigator = realmNavigator;
            this.realmController = realmController;
            this.mapRenderer = mapRenderer;
            this.roadAssetsPool = roadAssetsPool;
            this.satelliteFloor = satelliteFloor;
        }

        protected override async UniTask InternalExecuteAsync(TeleportParams teleportParams, CancellationToken ct)
        {
            float finalizationProgress =
                teleportParams.LoadingStatus.SetCurrentStage(LoadingStatus.LoadingStage.RealmChanging);


            await realmController.SetRealmAsync(teleportParams.CurrentDestinationRealm, ct);
            SwitchMiscVisibilityAsync();
            teleportParams.ParentReport.SetProgress(finalizationProgress);
        }

        public void SwitchMiscVisibilityAsync()
        {
            var type = realmController.Type;
            bool isGenesis = type is RealmType.GenesisCity;

            realmNavigator.ForceNotifyRealmChanged(type);
            mapRenderer.SetSharedLayer(MapLayer.PlayerMarker, isGenesis);
            satelliteFloor.SetCurrentlyInGenesis(isGenesis);
            roadAssetsPool.SwitchVisibility(isGenesis);
        }
    }
}
