using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.SearchResults;
using DCL.Navmap;
using DCL.PlacesAPIService;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.ComponentsFactory
{
    internal struct SearchResultsMarkersInstaller
    {
        private const int PREWARM_COUNT = 60;

        private Dictionary<MapLayer, IMapLayerController> writer;
        private IAssetsProvisioner assetsProvisioner;
        private IMapRendererSettings mapSettings;

        public async UniTask InstallAsync(
            Dictionary<MapLayer, IMapLayerController> layerWriter,
            List<IZoomScalingLayer> zoomScalingWriter,
            MapRendererConfiguration configuration,
            ICoordsUtils coordsUtils,
            IAssetsProvisioner assetsProv,
            IMapRendererSettings settings,
            IMapCullingController cullingController,
            INavmapBus navmapBus,
            CancellationToken cancellationToken
        )
        {
            mapSettings = settings;
            assetsProvisioner = assetsProv;
            this.writer = layerWriter;
            SearchResultMarkerObject? prefab = await GetPrefabAsync(cancellationToken);

            var objectsPool = new ObjectPool<SearchResultMarkerObject>(
                () => CreatePoolMethod(configuration, prefab, coordsUtils),
                defaultCapacity: PREWARM_COUNT,
                actionOnGet: obj => obj.gameObject.SetActive(true),
                actionOnRelease: obj => obj.gameObject.SetActive(false));

            var controller = new SearchResultMarkersController(
                objectsPool,
                CreateMarker,
                configuration.ScenesOfInterestMarkersRoot,
                coordsUtils,
                cullingController,
                navmapBus
            );

            await controller.InitializeAsync(cancellationToken);
            writer.Add(MapLayer.SearchResults, controller);
            zoomScalingWriter.Add(controller);
        }

        private static SearchResultMarkerObject CreatePoolMethod(MapRendererConfiguration configuration, SearchResultMarkerObject prefab, ICoordsUtils coordsUtils)
        {
            SearchResultMarkerObject searchResultMarkerObject = Object.Instantiate(prefab, configuration.SearchResultsMarkersRoot);

            for (var i = 0; i < searchResultMarkerObject.renderers.Length; i++)
                searchResultMarkerObject.renderers[i].sortingOrder = MapRendererDrawOrder.SEARCH_RESULTS;

            searchResultMarkerObject.title.sortingOrder = MapRendererDrawOrder.SEARCH_RESULTS;
            coordsUtils.SetObjectScale(searchResultMarkerObject);
            return searchResultMarkerObject;
        }

        private static ISearchResultMarker CreateMarker(IObjectPool<SearchResultMarkerObject> objectsPool, IMapCullingController cullingController, ICoordsUtils coordsUtils) =>
            new SearchResultMarker(objectsPool, cullingController, coordsUtils);

        private async UniTask<SearchResultMarkerObject> GetPrefabAsync(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.SearchResultMarker, cancellationToken)).Value;
    }
}
