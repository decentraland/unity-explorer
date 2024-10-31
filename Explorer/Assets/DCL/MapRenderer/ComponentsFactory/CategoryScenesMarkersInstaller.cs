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

            var objectsPool = new ObjectPool<CategoryMarkerObject>(
                () => CreatePoolMethod(configuration, prefab, coordsUtils),
                defaultCapacity: PREWARM_COUNT,
                actionOnGet: obj => obj.gameObject.SetActive(true),
                actionOnRelease: obj => obj.gameObject.SetActive(false));

            var artController = new CategoryMarkersController(placesAPIService, objectsPool, CreateMarker, configuration.CategoriesMarkersRoot, coordsUtils, cullingController, mapSettings.CategoryIconMappings, MapLayer.Art);
            var gameController = new CategoryMarkersController(placesAPIService, objectsPool, CreateMarker, configuration.CategoriesMarkersRoot, coordsUtils, cullingController, mapSettings.CategoryIconMappings, MapLayer.Game);
            var cryptoController = new CategoryMarkersController(placesAPIService, objectsPool, CreateMarker, configuration.CategoriesMarkersRoot, coordsUtils, cullingController, mapSettings.CategoryIconMappings, MapLayer.Crypto);
            var educationController = new CategoryMarkersController(placesAPIService, objectsPool, CreateMarker, configuration.CategoriesMarkersRoot, coordsUtils, cullingController, mapSettings.CategoryIconMappings, MapLayer.Education);
            var socialController = new CategoryMarkersController(placesAPIService, objectsPool, CreateMarker, configuration.CategoriesMarkersRoot, coordsUtils, cullingController, mapSettings.CategoryIconMappings, MapLayer.Social);
            var businessController = new CategoryMarkersController(placesAPIService, objectsPool, CreateMarker, configuration.CategoriesMarkersRoot, coordsUtils, cullingController, mapSettings.CategoryIconMappings, MapLayer.Business);
            var casinoController = new CategoryMarkersController(placesAPIService, objectsPool, CreateMarker, configuration.CategoriesMarkersRoot, coordsUtils, cullingController, mapSettings.CategoryIconMappings, MapLayer.Casino);
            var fashionController = new CategoryMarkersController(placesAPIService, objectsPool, CreateMarker, configuration.CategoriesMarkersRoot, coordsUtils, cullingController, mapSettings.CategoryIconMappings, MapLayer.Fashion);
            var musicController = new CategoryMarkersController(placesAPIService, objectsPool, CreateMarker, configuration.CategoriesMarkersRoot, coordsUtils, cullingController, mapSettings.CategoryIconMappings, MapLayer.Music);
            var shopController = new CategoryMarkersController(placesAPIService, objectsPool, CreateMarker, configuration.CategoriesMarkersRoot, coordsUtils, cullingController, mapSettings.CategoryIconMappings, MapLayer.Shop);
            var sportsController = new CategoryMarkersController(placesAPIService, objectsPool, CreateMarker, configuration.CategoriesMarkersRoot, coordsUtils, cullingController, mapSettings.CategoryIconMappings, MapLayer.Sports);

            await InitializeController(artController, MapLayer.Art, cancellationToken);
            zoomScalingWriter.Add(artController);
            await InitializeController(gameController, MapLayer.Game, cancellationToken);
            zoomScalingWriter.Add(gameController);
            await InitializeController(cryptoController, MapLayer.Crypto, cancellationToken);
            zoomScalingWriter.Add(cryptoController);
            await InitializeController(educationController, MapLayer.Education, cancellationToken);
            zoomScalingWriter.Add(educationController);
            await InitializeController(socialController, MapLayer.Social, cancellationToken);
            zoomScalingWriter.Add(socialController);
            await InitializeController(businessController, MapLayer.Business, cancellationToken);
            zoomScalingWriter.Add(businessController);
            await InitializeController(casinoController, MapLayer.Casino, cancellationToken);
            zoomScalingWriter.Add(casinoController);
            await InitializeController(fashionController, MapLayer.Fashion, cancellationToken);
            zoomScalingWriter.Add(fashionController);
            await InitializeController(musicController, MapLayer.Music, cancellationToken);
            zoomScalingWriter.Add(musicController);
            await InitializeController(shopController, MapLayer.Shop, cancellationToken);
            zoomScalingWriter.Add(shopController);
            await InitializeController(sportsController, MapLayer.Sports, cancellationToken);
            zoomScalingWriter.Add(sportsController);
        }

        private async UniTask InitializeController(IMapLayerController controller, MapLayer layer, CancellationToken cancellationToken)
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

        private static ICategoryMarker CreateMarker(IObjectPool<CategoryMarkerObject> objectsPool, IMapCullingController cullingController) =>
            new CategoryMarker(objectsPool, cullingController);

        private async UniTask<CategoryMarkerObject> GetPrefabAsync(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.CategoryMarker, cancellationToken)).Value;
    }
}
