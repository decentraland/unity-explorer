﻿using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Cluster;
using DCL.MapRenderer.MapLayers.PointsOfInterest;
using DCL.Navmap;
using DCL.PlacesAPIService;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.ComponentsFactory
{
    internal struct SceneOfInterestsMarkersInstaller
    {
        private IAssetsProvisioner assetsProvisioner;
        private IMapRendererSettings mapSettings;
        private IPlacesAPIService placesAPIService;

        public async UniTask<IMapLayerController> InstallAsync(
            Dictionary<MapLayer, IMapLayerController> writer,
            List<IZoomScalingLayer> zoomScalingWriter,
            MapRendererConfiguration configuration,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            IAssetsProvisioner assetsProv,
            IMapRendererSettings settings,
            IPlacesAPIService placesAPI,
            ObjectPool<ClusterMarkerObject> clusterObjectsPool,
            INavmapBus navmapBus,
            CancellationToken cancellationToken
        )
        {
            mapSettings = settings;
            assetsProvisioner = assetsProv;
            placesAPIService = placesAPI;
            SceneOfInterestMarkerObject? prefab = await GetPrefabAsync(cancellationToken);

            var objectsPool = new ObjectPool<SceneOfInterestMarkerObject>(
                () => CreatePoolMethod(configuration, prefab, coordsUtils),
                actionOnGet: obj => obj.gameObject.SetActive(true),
                actionOnRelease: obj => obj.gameObject.SetActive(false));

            var clusterController = new ClusterController(cullingController, clusterObjectsPool, ClusterHelper.CreateClusterMarker, coordsUtils, navmapBus);
            clusterController.SetClusterIcon(mapSettings.CategoryIconMappings.GetCategoryImage(MapLayer.ScenesOfInterest));

            var controller = new ScenesOfInterestMarkersController(
                placesAPIService,
                objectsPool,
                CreateMarker,
                configuration.ScenesOfInterestMarkersRoot,
                coordsUtils,
                cullingController,
                clusterController,
                navmapBus
            );

            await controller.InitializeAsync(cancellationToken);
            writer.Add(MapLayer.ScenesOfInterest, controller);
            zoomScalingWriter.Add(controller);
            return controller;
        }

        private static SceneOfInterestMarkerObject CreatePoolMethod(MapRendererConfiguration configuration, SceneOfInterestMarkerObject prefab, ICoordsUtils coordsUtils)
        {
            SceneOfInterestMarkerObject sceneOfInterestMarkerObject = Object.Instantiate(prefab, configuration.ScenesOfInterestMarkersRoot);

            for (var i = 0; i < sceneOfInterestMarkerObject.renderers.Length; i++)
                sceneOfInterestMarkerObject.renderers[i].sortingOrder = MapRendererDrawOrder.SCENES_OF_INTEREST;

            sceneOfInterestMarkerObject.title.sortingOrder = MapRendererDrawOrder.SCENES_OF_INTEREST;
            coordsUtils.SetObjectScale(sceneOfInterestMarkerObject);
            return sceneOfInterestMarkerObject;
        }

        private static ISceneOfInterestMarker CreateMarker(IObjectPool<SceneOfInterestMarkerObject> objectsPool, IMapCullingController cullingController, ICoordsUtils coordsUtils) =>
            new SceneOfInterestMarker(objectsPool, cullingController, coordsUtils);

        private async UniTask<SceneOfInterestMarkerObject> GetPrefabAsync(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.SceneOfInterestMarker, cancellationToken)).Value;
    }
}
