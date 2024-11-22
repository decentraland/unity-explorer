using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.EventsApi;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Categories;
using DCL.PlacesAPIService;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.ComponentsFactory
{
    internal struct LiveEventsMarkersInstaller
    {
        private const int PREWARM_COUNT = 60;

        private Dictionary<MapLayer, IMapLayerController> writer;
        private IAssetsProvisioner assetsProvisioner;
        private IMapRendererSettings mapSettings;
        private IEventsApiService eventsApiService;

        public async UniTask InstallAsync(
            Dictionary<MapLayer, IMapLayerController> layerWriter,
            List<IZoomScalingLayer> zoomScalingWriter,
            MapRendererConfiguration configuration,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            IAssetsProvisioner assetsProv,
            IMapRendererSettings settings,
            IEventsApiService eventsApi,
            ObjectPool<ClusterMarkerObject> clusterObjectsPool,
            CategoryMarkerObject prefab,
            CancellationToken cancellationToken
        )
        {
            mapSettings = settings;
            assetsProvisioner = assetsProv;
            eventsApiService = eventsApi;
            this.writer = layerWriter;

            var objectsPool = new ObjectPool<CategoryMarkerObject>(
                () => CreatePoolMethod(configuration, prefab, coordsUtils),
                defaultCapacity: PREWARM_COUNT,
                actionOnGet: obj => obj.gameObject.SetActive(true),
                actionOnRelease: obj => obj.gameObject.SetActive(false));

            LiveEventsMarkersController liveEventsMarkersController = new LiveEventsMarkersController(
                eventsApiService,
                objectsPool,
                CreateMarker,
                configuration.LiveEventsMarkersRoot,
                coordsUtils,
                cullingController,
                mapSettings.CategoryIconMappings,
                MapLayer.LiveEvents,
                new ClusterController(cullingController, clusterObjectsPool, CreateClusterMarker, coordsUtils, MapLayer.LiveEvents, mapSettings.CategoryIconMappings)
            );

            await liveEventsMarkersController.InitializeAsync(cancellationToken);
            writer.Add(MapLayer.LiveEvents, liveEventsMarkersController);
            zoomScalingWriter.Add(liveEventsMarkersController);
        }

        private static CategoryMarkerObject CreatePoolMethod(MapRendererConfiguration configuration, CategoryMarkerObject prefab, ICoordsUtils coordsUtils)
        {
            CategoryMarkerObject categoryMarkerObject = Object.Instantiate(prefab, configuration.LiveEventsMarkersRoot);

            for (var i = 0; i < categoryMarkerObject.renderers.Length; i++)
                categoryMarkerObject.renderers[i].sortingOrder = MapRendererDrawOrder.LIVE_EVENTS;

            categoryMarkerObject.title.sortingOrder = MapRendererDrawOrder.LIVE_EVENTS;
            coordsUtils.SetObjectScale(categoryMarkerObject);
            return categoryMarkerObject;
        }

        private static ICategoryMarker CreateMarker(IObjectPool<CategoryMarkerObject> objectsPool, IMapCullingController cullingController, ICoordsUtils coordsUtils) =>
            new CategoryMarker(objectsPool, cullingController, coordsUtils);

        private static IClusterMarker CreateClusterMarker(IObjectPool<ClusterMarkerObject> objectsPool, IMapCullingController cullingController, ICoordsUtils coordsUtils) =>
            new ClusterMarker(objectsPool, cullingController, coordsUtils);
    }
}
