using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
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
    internal struct CategoryScenesMarkersInstaller
    {
        private const int PREWARM_COUNT = 60;

        private Dictionary<MapLayer, IMapLayerController> writer;
        private IAssetsProvisioner assetsProvisioner;
        private IMapRendererSettings mapSettings;
        private IPlacesAPIService placesAPIService;

        public async UniTask InstallAsync(
            Dictionary<MapLayer, IMapLayerController> layerWriter,
            List<IZoomScalingLayer> zoomScalingWriter,
            MapRendererConfiguration configuration,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            IAssetsProvisioner assetsProv,
            IMapRendererSettings settings,
            IPlacesAPIService placesAPI,
            CancellationToken cancellationToken
        )
        {
            mapSettings = settings;
            assetsProvisioner = assetsProv;
            placesAPIService = placesAPI;
            this.writer = layerWriter;
            CategoryMarkerObject? prefab = await GetPrefabAsync(cancellationToken);
            ClusterMarkerObject? clusterPrefab = await GetClusterPrefabAsync(cancellationToken);

            var objectsPool = new ObjectPool<CategoryMarkerObject>(
                () => CreatePoolMethod(configuration, prefab, coordsUtils),
                defaultCapacity: PREWARM_COUNT,
                actionOnGet: obj => obj.gameObject.SetActive(true),
                actionOnRelease: obj => obj.gameObject.SetActive(false));

            var clusterObjectsPool = new ObjectPool<ClusterMarkerObject>(
                () => CreateClusterPoolMethod(configuration, clusterPrefab, coordsUtils),
                defaultCapacity: PREWARM_COUNT,
                actionOnGet: obj => obj.gameObject.SetActive(true),
                actionOnRelease: obj => obj.gameObject.SetActive(false));

            var artController = new CategoryMarkersController(placesAPIService, objectsPool, CreateMarker, clusterObjectsPool, CreateClusterMarker, configuration.CategoriesMarkersRoot, coordsUtils, cullingController, mapSettings.CategoryIconMappings, MapLayer.Art);
            var gameController = new CategoryMarkersController(placesAPIService, objectsPool, CreateMarker, clusterObjectsPool, CreateClusterMarker, configuration.CategoriesMarkersRoot, coordsUtils, cullingController, mapSettings.CategoryIconMappings, MapLayer.Game);
            var cryptoController = new CategoryMarkersController(placesAPIService, objectsPool, CreateMarker, clusterObjectsPool, CreateClusterMarker, configuration.CategoriesMarkersRoot, coordsUtils, cullingController, mapSettings.CategoryIconMappings, MapLayer.Crypto);
            var educationController = new CategoryMarkersController(placesAPIService, objectsPool, CreateMarker, clusterObjectsPool, CreateClusterMarker, configuration.CategoriesMarkersRoot, coordsUtils, cullingController, mapSettings.CategoryIconMappings, MapLayer.Education);
            var socialController = new CategoryMarkersController(placesAPIService, objectsPool, CreateMarker, clusterObjectsPool, CreateClusterMarker, configuration.CategoriesMarkersRoot, coordsUtils, cullingController, mapSettings.CategoryIconMappings, MapLayer.Social);
            var businessController = new CategoryMarkersController(placesAPIService, objectsPool, CreateMarker, clusterObjectsPool, CreateClusterMarker, configuration.CategoriesMarkersRoot, coordsUtils, cullingController, mapSettings.CategoryIconMappings, MapLayer.Business);
            var casinoController = new CategoryMarkersController(placesAPIService, objectsPool, CreateMarker, clusterObjectsPool, CreateClusterMarker, configuration.CategoriesMarkersRoot, coordsUtils, cullingController, mapSettings.CategoryIconMappings, MapLayer.Casino);
            var fashionController = new CategoryMarkersController(placesAPIService, objectsPool, CreateMarker, clusterObjectsPool, CreateClusterMarker, configuration.CategoriesMarkersRoot, coordsUtils, cullingController, mapSettings.CategoryIconMappings, MapLayer.Fashion);
            var musicController = new CategoryMarkersController(placesAPIService, objectsPool, CreateMarker, clusterObjectsPool, CreateClusterMarker, configuration.CategoriesMarkersRoot, coordsUtils, cullingController, mapSettings.CategoryIconMappings, MapLayer.Music);
            var shopController = new CategoryMarkersController(placesAPIService, objectsPool, CreateMarker, clusterObjectsPool, CreateClusterMarker, configuration.CategoriesMarkersRoot, coordsUtils, cullingController, mapSettings.CategoryIconMappings, MapLayer.Shop);
            var sportsController = new CategoryMarkersController(placesAPIService, objectsPool, CreateMarker, clusterObjectsPool, CreateClusterMarker, configuration.CategoriesMarkersRoot, coordsUtils, cullingController, mapSettings.CategoryIconMappings, MapLayer.Sports);

            await InitializeControllerAsync(artController, MapLayer.Art, cancellationToken);
            zoomScalingWriter.Add(artController);
            await InitializeControllerAsync(gameController, MapLayer.Game, cancellationToken);
            zoomScalingWriter.Add(gameController);
            await InitializeControllerAsync(cryptoController, MapLayer.Crypto, cancellationToken);
            zoomScalingWriter.Add(cryptoController);
            await InitializeControllerAsync(educationController, MapLayer.Education, cancellationToken);
            zoomScalingWriter.Add(educationController);
            await InitializeControllerAsync(socialController, MapLayer.Social, cancellationToken);
            zoomScalingWriter.Add(socialController);
            await InitializeControllerAsync(businessController, MapLayer.Business, cancellationToken);
            zoomScalingWriter.Add(businessController);
            await InitializeControllerAsync(casinoController, MapLayer.Casino, cancellationToken);
            zoomScalingWriter.Add(casinoController);
            await InitializeControllerAsync(fashionController, MapLayer.Fashion, cancellationToken);
            zoomScalingWriter.Add(fashionController);
            await InitializeControllerAsync(musicController, MapLayer.Music, cancellationToken);
            zoomScalingWriter.Add(musicController);
            await InitializeControllerAsync(shopController, MapLayer.Shop, cancellationToken);
            zoomScalingWriter.Add(shopController);
            await InitializeControllerAsync(sportsController, MapLayer.Sports, cancellationToken);
            zoomScalingWriter.Add(sportsController);
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

        private static ClusterMarkerObject CreateClusterPoolMethod(MapRendererConfiguration configuration, ClusterMarkerObject prefab, ICoordsUtils coordsUtils)
        {
            ClusterMarkerObject categoryMarkerObject = Object.Instantiate(prefab, configuration.CategoriesMarkersRoot);

            for (var i = 0; i < categoryMarkerObject.renderers.Length; i++)
                categoryMarkerObject.renderers[i].sortingOrder = MapRendererDrawOrder.CATEGORIES;

            categoryMarkerObject.title.sortingOrder = MapRendererDrawOrder.CATEGORIES + 1;
            coordsUtils.SetObjectScale(categoryMarkerObject);
            return categoryMarkerObject;
        }

        private static ICategoryMarker CreateMarker(IObjectPool<CategoryMarkerObject> objectsPool, IMapCullingController cullingController, ICoordsUtils coordsUtils) =>
            new CategoryMarker(objectsPool, cullingController, coordsUtils);

        private static IClusterMarker CreateClusterMarker(IObjectPool<ClusterMarkerObject> objectsPool, IMapCullingController cullingController, ICoordsUtils coordsUtils) =>
            new ClusterMarker(objectsPool, cullingController, coordsUtils);

        private async UniTask<CategoryMarkerObject> GetPrefabAsync(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.CategoryMarker, cancellationToken)).Value;

        private async UniTask<ClusterMarkerObject> GetClusterPrefabAsync(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.ClusterMarker, cancellationToken)).Value;
    }
}
