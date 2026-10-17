using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Categories;
using DCL.MapRenderer.MapLayers.Cluster;
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
        private Dictionary<MapLayer, IMapLayerController> writer;
        private IAssetsProvisioner assetsProvisioner;
        private IMapRendererSettings mapSettings;

        public async UniTask<IMapLayerController> InstallAsync(
            Dictionary<MapLayer, IMapLayerController> layerWriter,
            List<IZoomScalingLayer> zoomScalingWriter,
            MapRendererConfiguration configuration,
            ICoordsUtils coordsUtils,
            IAssetsProvisioner assetsProv,
            IMapRendererSettings settings,
            IMapCullingController cullingController,
            ObjectPool<ClusterMarkerObject> clusterObjectsPool,
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
                actionOnGet: obj => obj.gameObject.SetActive(true),
                actionOnRelease: obj => obj.gameObject.SetActive(false));

            var clusterController = new ClusterController(cullingController, clusterObjectsPool, ClusterHelper.CreateClusterMarker, coordsUtils, navmapBus);
            clusterController.SetClusterIcon(mapSettings.CategoryIconMappings.GetCategoryImage(MapLayer.SearchResults));

            var controller = new SearchResultMarkersController(
                objectsPool,
                CreateMarker,
                configuration.ScenesOfInterestMarkersRoot,
                coordsUtils,
                cullingController,
                navmapBus,
                clusterController
            );

            await controller.InitializeAsync(cancellationToken);
            writer.Add(MapLayer.SearchResults, controller);
            zoomScalingWriter.Add(controller);
            return controller;
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
