using Cysharp.Threading.Tasks;
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
    internal struct CategoryScenesMarkersInstaller
    {
        private const int PREWARM_COUNT = 60;

        private Dictionary<MapLayer, IMapLayerController> writer;
        private IMapRendererSettings mapSettings;

        public async UniTask<IMapLayerController> InstallAsync(
            Dictionary<MapLayer, IMapLayerController> layerWriter,
            List<IZoomScalingLayer> zoomScalingWriter,
            MapRendererConfiguration configuration,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            IMapRendererSettings settings,
            ObjectPool<ClusterMarkerObject> clusterObjectsPool,
            CategoryMarkerObject prefab,
            INavmapBus navmapBus,
            CancellationToken cancellationToken
        )
        {
            mapSettings = settings;
            this.writer = layerWriter;

            var objectsPool = new ObjectPool<CategoryMarkerObject>(
                () => CreatePoolMethod(configuration, prefab, coordsUtils),
                defaultCapacity: PREWARM_COUNT,
                actionOnGet: obj => obj.gameObject.SetActive(true),
                actionOnRelease: obj => obj.gameObject.SetActive(false));

            var categoryMarkersController = new CategoryMarkersController(
                objectsPool,
                CreateMarker,
                configuration.CategoriesMarkersRoot,
                coordsUtils,
                cullingController,
                mapSettings.CategoryLayerIconMappings,
                new ClusterController(cullingController, clusterObjectsPool, ClusterHelper.CreateClusterMarker, coordsUtils, navmapBus),
                navmapBus
            );

            await InitializeControllerAsync(categoryMarkersController, MapLayer.Category, cancellationToken);
            zoomScalingWriter.Add(categoryMarkersController);

            return categoryMarkersController;
        }

        private async UniTask InitializeControllerAsync(IMapLayerController controller, MapLayer layer, CancellationToken cancellationToken)
        {
            await controller.InitializeAsync(cancellationToken);
            writer.Add(layer, controller);
        }

        private static CategoryMarkerObject CreatePoolMethod(MapRendererConfiguration configuration, CategoryMarkerObject prefab, ICoordsUtils coordsUtils)
        {
            CategoryMarkerObject categoryMarkerObject = Object.Instantiate(prefab, configuration.CategoriesMarkersRoot);

            for (var i = 0; i < categoryMarkerObject.renderers.Length; i++)
                categoryMarkerObject.renderers[i].sortingOrder = MapRendererDrawOrder.CATEGORIES;

            categoryMarkerObject.title.sortingOrder = MapRendererDrawOrder.CATEGORIES;
            coordsUtils.SetObjectScale(categoryMarkerObject);
            return categoryMarkerObject;
        }

        private static ICategoryMarker CreateMarker(IObjectPool<CategoryMarkerObject> objectsPool, IMapCullingController cullingController, ICoordsUtils coordsUtils) =>
            new CategoryMarker(objectsPool, cullingController, coordsUtils);
    }
}
