﻿using Cysharp.Threading.Tasks;
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
            ObjectPool<ClusterMarkerObject> clusterObjectsPool,
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

            var artController = CreateCategoryController(MapLayer.Art, objectsPool, clusterObjectsPool, configuration, coordsUtils, cullingController);
            var gameController = CreateCategoryController(MapLayer.Game, objectsPool, clusterObjectsPool, configuration, coordsUtils, cullingController);
            var cryptoController = CreateCategoryController(MapLayer.Crypto, objectsPool, clusterObjectsPool, configuration, coordsUtils, cullingController);
            var educationController = CreateCategoryController(MapLayer.Education, objectsPool, clusterObjectsPool, configuration, coordsUtils, cullingController);
            var socialController = CreateCategoryController(MapLayer.Social, objectsPool, clusterObjectsPool, configuration, coordsUtils, cullingController);
            var businessController = CreateCategoryController(MapLayer.Business, objectsPool, clusterObjectsPool, configuration, coordsUtils, cullingController);
            var casinoController = CreateCategoryController(MapLayer.Casino, objectsPool, clusterObjectsPool, configuration, coordsUtils, cullingController);
            var fashionController = CreateCategoryController(MapLayer.Fashion, objectsPool, clusterObjectsPool, configuration, coordsUtils, cullingController);
            var musicController = CreateCategoryController(MapLayer.Music, objectsPool, clusterObjectsPool, configuration, coordsUtils, cullingController);
            var shopController = CreateCategoryController(MapLayer.Shop, objectsPool, clusterObjectsPool, configuration, coordsUtils, cullingController);
            var sportsController = CreateCategoryController(MapLayer.Sports, objectsPool, clusterObjectsPool, configuration, coordsUtils, cullingController);

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

        private CategoryMarkersController CreateCategoryController(MapLayer layer, ObjectPool<CategoryMarkerObject> objectsPool, ObjectPool<ClusterMarkerObject> clusterObjectsPool, MapRendererConfiguration configuration, ICoordsUtils coordsUtils, IMapCullingController cullingController)
        {
            return new CategoryMarkersController(
                placesAPIService,
                objectsPool,
                CreateMarker,
                configuration.CategoriesMarkersRoot,
                coordsUtils,
                cullingController,
                mapSettings.CategoryIconMappings,
                layer,
                new ClusterController(cullingController, clusterObjectsPool, CreateClusterMarker, coordsUtils, layer, mapSettings.CategoryIconMappings)
            );
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

        private static IClusterMarker CreateClusterMarker(IObjectPool<ClusterMarkerObject> objectsPool, IMapCullingController cullingController, ICoordsUtils coordsUtils) =>
            new ClusterMarker(objectsPool, cullingController, coordsUtils);

        private async UniTask<CategoryMarkerObject> GetPrefabAsync(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.CategoryMarker, cancellationToken)).Value;
    }
}