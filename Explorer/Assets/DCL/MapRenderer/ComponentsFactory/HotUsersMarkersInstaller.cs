using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Users;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.ComponentsFactory
{
    internal struct HotUsersMarkersInstaller
    {
        private const int HOT_USER_MARKERS_PREWARM_COUNT = 30;

        private IAssetsProvisioner assetsProvisioner;
        private MapRendererSettings mapSettings;

        public async UniTask InstallAsync(
            Dictionary<MapLayer, IMapLayerController> writer,
            MapRendererConfiguration configuration,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            IAssetsProvisioner assetsProv,
            MapRendererSettings settings,
            CancellationToken cancellationToken)
        {
            assetsProvisioner = assetsProv;
            mapSettings = settings;
            var prefab = await GetPrefabAsync(cancellationToken);

            var objectsPool = new ObjectPool<HotUserMarkerObject>(
                () => CreatePoolMethod(configuration, prefab, coordsUtils),
                actionOnGet: (obj) => obj.gameObject.SetActive(true),
                actionOnRelease: (obj) => obj.gameObject.SetActive(false),
                defaultCapacity: HOT_USER_MARKERS_PREWARM_COUNT);

            IHotUserMarker CreateWrap() =>
                new HotUserMarker(objectsPool, coordsUtils);

            var wrapsPool = new ObjectPool<IHotUserMarker>(CreateWrap, actionOnRelease: m => m.Dispose(), defaultCapacity: HOT_USER_MARKERS_PREWARM_COUNT);

            var controller = new UsersMarkersHotAreaController(objectsPool, wrapsPool, configuration.HotUserMarkersRoot, coordsUtils, cullingController);

            writer.Add(MapLayer.HotUsersMarkers, controller);
        }

        private static HotUserMarkerObject CreatePoolMethod(MapRendererConfiguration configuration, HotUserMarkerObject prefab, ICoordsUtils coordsUtils)
        {
            HotUserMarkerObject markerObject = Object.Instantiate(prefab, configuration.HotUserMarkersRoot);
            for (var i = 0; i < markerObject.spriteRenderers.Length; i++)
                markerObject.spriteRenderers[i].sortingOrder = MapRendererDrawOrder.HOT_USER_MARKERS;

            coordsUtils.SetObjectScale(markerObject);
            return markerObject;
        }
        private async UniTask<HotUserMarkerObject> GetPrefabAsync(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.UserMarker, cancellationToken)).Value.GetComponent<HotUserMarkerObject>();
    }
}
