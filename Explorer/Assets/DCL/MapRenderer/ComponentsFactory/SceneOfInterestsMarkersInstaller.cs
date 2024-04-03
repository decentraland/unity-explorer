using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.PointsOfInterest;
using DCL.PlacesAPIService;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.ComponentsFactory
{
    internal struct SceneOfInterestsMarkersInstaller
    {
        private const int PREWARM_COUNT = 60;

        private IAssetsProvisioner assetsProvisioner;
        private MapRendererSettings mapSettings;
        private IPlacesAPIService placesAPIService;

        public async UniTask InstallAsync(
            Dictionary<MapLayer, IMapLayerController> writer,
            List<IZoomScalingLayer> zoomScalingWriter,
            MapRendererConfiguration configuration,
            ICoordsUtils coordsUtils,
            IMapCullingController cullingController,
            IAssetsProvisioner assetsProv,
            MapRendererSettings settings,
            IPlacesAPIService placesAPI,
            CancellationToken cancellationToken
        )
        {
            this.mapSettings = settings;
            this.assetsProvisioner = assetsProv;
            this.placesAPIService = placesAPI;
            var prefab = await GetPrefab(cancellationToken);

            var objectsPool = new ObjectPool<SceneOfInterestMarkerObject>(
                () => CreatePoolMethod(configuration, prefab, coordsUtils),
                defaultCapacity: PREWARM_COUNT);

            var controller = new ScenesOfInterestMarkersController(
                placesAPIService,
                objectsPool,
                CreateMarker,
                configuration.ScenesOfInterestMarkersRoot,
                coordsUtils,
                cullingController
            );

            await controller.Initialize(cancellationToken);
            writer.Add(MapLayer.ScenesOfInterest, controller);
            zoomScalingWriter.Add(controller);
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

        private static ISceneOfInterestMarker CreateMarker(IObjectPool<SceneOfInterestMarkerObject> objectsPool, IMapCullingController cullingController) =>
            new SceneOfInterestMarker(objectsPool, cullingController);

        private async UniTask<SceneOfInterestMarkerObject> GetPrefab(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.SceneOfInterestMarker, cancellationToken)).Value.GetComponent<SceneOfInterestMarkerObject>();
    }
}
