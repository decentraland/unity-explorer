using DCL.Landscape;
using DCL.LOD;
using DCL.MapRenderer;
using DCL.MapRenderer.MapLayers;
using DCL.Minimap;
using ECS.SceneLifeCycle.Realm;
using System;

namespace Global.Dynamic.Misc
{
    public class RealmMisc : IRealmMisc
    {
        private readonly IMapRenderer mapRenderer;
        private readonly RoadAssetsPool roadAssetsPool;
        private readonly SatelliteFloor satelliteFloor;
        private readonly Lazy<MinimapController> minimap;

        public RealmMisc(IMapRenderer mapRenderer, RoadAssetsPool roadAssetsPool, SatelliteFloor satelliteFloor, Lazy<MinimapController> minimap)
        {
            this.mapRenderer = mapRenderer;
            this.roadAssetsPool = roadAssetsPool;
            this.satelliteFloor = satelliteFloor;
            this.minimap = minimap;
        }

        public void SwitchTo(RealmType realmType)
        {
            bool isGenesis = realmType is RealmType.GenesisCity;

            minimap.Value!.OnRealmChanged(realmType);
            mapRenderer.SetSharedLayer(MapLayer.PlayerMarker, isGenesis);
            satelliteFloor.SetCurrentlyInGenesis(isGenesis);
            roadAssetsPool.SwitchVisibility(isGenesis);
        }
    }
}
