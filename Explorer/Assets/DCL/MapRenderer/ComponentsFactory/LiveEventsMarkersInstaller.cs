using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.EventsApi;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Categories;
using DCL.MapRenderer.MapLayers.Cluster;
using DCL.Navmap;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.ComponentsFactory
{
    internal struct LiveEventsMarkersInstaller
    {
        private Dictionary<MapLayer, IMapLayerController> writer;
        private IMapRendererSettings mapSettings;
        private HttpEventsApiService eventsApiService;

        public async UniTask<IMapLayerController> InstallAsync(
            Dictionary<MapLayer, IMapLayerController> layerWriter,
            List<IZoomScalingLayer> zoomScalingWriter,
            MapRendererConfiguration configuration,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            IMapRendererSettings settings,
            HttpEventsApiService eventsApi,
            ObjectPool<ClusterMarkerObject> clusterObjectsPool,
            CategoryMarkerObject prefab,
            INavmapBus navmapBus,
            CancellationToken cancellationToken
        )
        {
            mapSettings = settings;
            eventsApiService = eventsApi;
            this.writer = layerWriter;

            var objectsPool = new ObjectPool<CategoryMarkerObject>(
                () => CreatePoolMethod(configuration, prefab, coordsUtils),
                actionOnGet: obj => obj.gameObject.SetActive(true),
                actionOnRelease: obj => obj.gameObject.SetActive(false));

            var clusterController = new ClusterController(cullingController, clusterObjectsPool, ClusterHelper.CreateClusterMarker, coordsUtils, navmapBus);
            clusterController.SetClusterIcon(mapSettings.CategoryIconMappings.GetCategoryImage(MapLayer.LiveEvents));

            LiveEventsMarkersController liveEventsMarkersController = new LiveEventsMarkersController(
                eventsApiService,
                objectsPool,
                CreateMarker,
                configuration.LiveEventsMarkersRoot,
                coordsUtils,
                cullingController,
                mapSettings.CategoryIconMappings,
                MapLayer.LiveEvents,
                clusterController,
                navmapBus
            );

            await liveEventsMarkersController.InitializeAsync(cancellationToken);
            writer.Add(MapLayer.LiveEvents, liveEventsMarkersController);
            zoomScalingWriter.Add(liveEventsMarkersController);
            return liveEventsMarkersController;
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
    }
}
