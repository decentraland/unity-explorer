using DCL.Landscape;
using DCL.LOD;
using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers;
using DCL.Minimap;
using DCL.Utilities.Extensions;
using ECS.SceneLifeCycle.Realm;

namespace Global.Dynamic.Misc
{
    public class RealmMisc : IRealmMisc
    {
        private readonly IMapRenderer mapRenderer;
        private readonly RoadAssetsPool roadAssetsPool;
        private readonly SatelliteFloor satelliteFloor;

        // This is a lazy reference to avoid circular dependencies in DynamicWorldContainer, evil hack should be redesigned
        private MinimapController minimap = null!;

        public RealmMisc(IMapRenderer mapRenderer, RoadAssetsPool roadAssetsPool, SatelliteFloor satelliteFloor)
        {
            this.mapRenderer = mapRenderer;
            this.roadAssetsPool = roadAssetsPool;
            this.satelliteFloor = satelliteFloor;
        }

        public void SwitchTo(RealmType realmType)
        {
            bool isGenesis = realmType is RealmType.GenesisCity;

            minimap.EnsureNotNull().OnRealmChanged(realmType);
            mapRenderer.SetSharedLayer(MapLayer.PlayerMarker, isGenesis);
            satelliteFloor.SetCurrentlyInGenesis(isGenesis);
            roadAssetsPool.SwitchVisibility(isGenesis);
        }

        public void Inject(MinimapController minimapController)
        {
            minimap = minimapController;
        }
    }
}
