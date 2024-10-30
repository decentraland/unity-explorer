using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Categories;
using DCL.MapRenderer.MapLayers.Categories.Art;
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

        private IAssetsProvisioner assetsProvisioner;
        private IMapRendererSettings mapSettings;
        private IPlacesAPIService placesAPIService;

        public async UniTask InstallAsync(
            Dictionary<MapLayer, IMapLayerController> writer,
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
            CategoryMarkerObject? prefab = await GetPrefabAsync(cancellationToken);

            var objectsPool = new ObjectPool<CategoryMarkerObject>(
                () => CreatePoolMethod(configuration, prefab, coordsUtils),
                defaultCapacity: PREWARM_COUNT,
                actionOnGet: obj => obj.gameObject.SetActive(true),
                actionOnRelease: obj => obj.gameObject.SetActive(false));

            var artController = new ArtCategoryMarkersController(
                placesAPIService,
                objectsPool,
                CreateMarker,
                configuration.CategoriesMarkersRoot,
                coordsUtils,
                cullingController,
                mapSettings.CategoryIconMappings
            );

            await artController.InitializeAsync(cancellationToken);
            writer.Add(MapLayer.Art, artController);
            zoomScalingWriter.Add(artController);
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
