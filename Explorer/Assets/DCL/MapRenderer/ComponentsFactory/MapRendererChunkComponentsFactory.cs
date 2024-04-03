using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Atlas;
using DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas;
using DCL.MapRenderer.MapLayers.ParcelHighlight;
using DCL.MapRenderer.MapLayers.SatelliteAtlas;
using DCL.PlacesAPIService;
using DCL.WebRequests;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;

namespace DCL.MapRenderer.ComponentsFactory
{
    public class MapRendererChunkComponentsFactory : IMapRendererComponentsFactory
    {
        private PlayerMarkerInstaller playerMarkerInstaller { get; }
        private SceneOfInterestsMarkersInstaller sceneOfInterestMarkerInstaller { get; }
        private FavoritesMarkersInstaller favoritesMarkersInstaller { get; }

        private readonly IAssetsProvisioner assetsProvisioner;

        private readonly IWebRequestController webRequestController;
        private readonly MapRendererTextureContainer textureContainer;
        private readonly IPlacesAPIService placesAPIService;
        private readonly MapRendererSettings mapSettings;

        public MapRendererChunkComponentsFactory(
            IAssetsProvisioner assetsProvisioner,
            MapRendererSettings settings,
            IWebRequestController webRequestController,
            MapRendererTextureContainer textureContainer,
            IPlacesAPIService placesAPIService)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mapSettings = settings;
            this.webRequestController = webRequestController;
            this.textureContainer = textureContainer;
            this.placesAPIService = placesAPIService;
        }

        async UniTask<MapRendererComponents> IMapRendererComponentsFactory.CreateAsync(CancellationToken cancellationToken)
        {
            MapRendererConfiguration configuration = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(mapSettings.MapRendererConfiguration, ct: CancellationToken.None)).Value.GetComponent<MapRendererConfiguration>());
            var coordsUtils = new ChunkCoordsUtils(MapRendererSettings.PARCEL_SIZE);
            IMapCullingController cullingController = new MapCullingController(new MapCullingRectVisibilityChecker(MapRendererSettings.CULLING_BOUNDS_IN_PARCELS * MapRendererSettings.PARCEL_SIZE));
            var layers = new Dictionary<MapLayer, IMapLayerController>();
            var zoomScalingLayers = new List<IZoomScalingLayer>();

            ParcelHighlightMarkerObject highlightMarkerPrefab = await GetParcelHighlightMarkerPrefabAsync(cancellationToken);

            var highlightMarkersPool = new ObjectPool<IParcelHighlightMarker>(
                () => CreateHighlightMarker(highlightMarkerPrefab, configuration, coordsUtils),
                _ => { },
                marker => marker.Deactivate(),
                marker => marker.Dispose(),
                defaultCapacity: 1
            );

            MapCameraObject mapCameraObjectPrefab = (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.MapCameraObject, ct: CancellationToken.None)).Value.GetComponent<MapCameraObject>();

            IObjectPool<IMapCameraControllerInternal> cameraControllersPool = new ObjectPool<IMapCameraControllerInternal>(
                CameraControllerBuilder,
                x => x.SetActive(true),
                x => x.SetActive(false),
                x => x.Dispose()
            );

            await UniTask.WhenAll(
                CreateAtlasAsync(layers, configuration, coordsUtils, cullingController, cancellationToken),
                CreateSatelliteAtlasAsync(layers, configuration, coordsUtils, cullingController, cancellationToken),
                playerMarkerInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, cullingController, mapSettings, assetsProvisioner, cancellationToken),
                sceneOfInterestMarkerInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, cullingController, assetsProvisioner, mapSettings, placesAPIService, cancellationToken),
                favoritesMarkersInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, cullingController, placesAPIService, assetsProvisioner, mapSettings, cancellationToken)
                /* List of other creators that can be executed in parallel */);

            return new MapRendererComponents(configuration, layers, zoomScalingLayers, cullingController, cameraControllersPool);

            IMapCameraControllerInternal CameraControllerBuilder()
            {
                MapCameraObject instance = Object.Instantiate(mapCameraObjectPrefab, configuration.MapCamerasRoot);
                var interactivityController = new MapCameraInteractivityController(configuration.MapCamerasRoot, instance.mapCamera, highlightMarkersPool, coordsUtils);

                return new MapCameraController.MapCameraController(interactivityController, instance, coordsUtils, cullingController);
            }
        }

        private async UniTask CreateSatelliteAtlasAsync(Dictionary<MapLayer, IMapLayerController> layers, MapRendererConfiguration configuration, ICoordsUtils coordsUtils, IMapCullingController cullingController, CancellationToken cancellationToken)
        {
            const int GRID_SIZE = 8; // satellite images are provided by 8x8 grid.
            const int PARCELS_INSIDE_CHUNK = 40; // One satellite image contains 40 parcels.

            var chunkAtlas = new SatelliteChunkAtlasController(configuration.SatelliteAtlasRoot, GRID_SIZE, PARCELS_INSIDE_CHUNK, coordsUtils, cullingController, chunkBuilder: CreateSatelliteChunkAsync);

            await chunkAtlas.InitializeAsync(cancellationToken).SuppressCancellationThrow();

            layers.Add(MapLayer.SatelliteAtlas, chunkAtlas);
            return;

            async UniTask<IChunkController> CreateSatelliteChunkAsync(Vector3 chunkLocalPosition, Vector2Int chunkId, Transform parent, CancellationToken ct)
            {
                SpriteRenderer atlasChunkPrefab = await GetAtlasChunkPrefabAsync(parent, ct);

                var chunk = new SatelliteChunkController(atlasChunkPrefab, webRequestController, textureContainer, chunkLocalPosition, chunkId, parent, MapRendererDrawOrder.SATELLITE_ATLAS);
                await chunk.LoadImageAsync(chunkId, PARCELS_INSIDE_CHUNK * coordsUtils.ParcelSize, ct);

                return chunk;
            }
        }

        private async UniTask CreateAtlasAsync(Dictionary<MapLayer, IMapLayerController> layers, MapRendererConfiguration configuration, ICoordsUtils coordsUtils, IMapCullingController cullingController, CancellationToken cancellationToken)
        {
            var chunkAtlas = new ParcelChunkAtlasController(configuration.AtlasRoot, MapRendererSettings.ATLAS_CHUNK_SIZE, coordsUtils, cullingController, chunkBuilder: CreateChunkAsync);

            // initialize Atlas but don't block the flow (to accelerate loading time)
            chunkAtlas.InitializeAsync(cancellationToken).SuppressCancellationThrow().Forget();

            layers.Add(MapLayer.ParcelsAtlas, chunkAtlas);
            return;

            async UniTask<IChunkController> CreateChunkAsync(Vector3 chunkLocalPosition, Vector2Int coordsCenter, Transform parent, CancellationToken ct)
            {
                SpriteRenderer atlasChunkPrefab = await GetAtlasChunkPrefabAsync(parent, ct);

                var chunk = new ParcelChunkController(webRequestController, atlasChunkPrefab, chunkLocalPosition, coordsCenter, parent);
                chunk.SetDrawOrder(MapRendererDrawOrder.ATLAS);
                await chunk.LoadImageAsync(MapRendererSettings.ATLAS_CHUNK_SIZE, MapRendererSettings.PARCEL_SIZE, coordsCenter, ct);

                return chunk;
            }
        }

        private static IParcelHighlightMarker CreateHighlightMarker(ParcelHighlightMarkerObject highlightMarkerPrefab,
            MapRendererConfiguration configuration, ICoordsUtils coordsUtils)
        {
            ParcelHighlightMarkerObject obj = Object.Instantiate(highlightMarkerPrefab, configuration.ParcelHighlightRoot);

            obj.spriteRenderer.sortingOrder = MapRendererDrawOrder.PARCEL_HIGHLIGHT;
            obj.text.sortingOrder = MapRendererDrawOrder.PARCEL_HIGHLIGHT;
            coordsUtils.SetObjectScale(obj);

            return new ParcelHighlightMarker(obj);
        }

        internal async Task<SpriteRenderer> GetAtlasChunkPrefabAsync(Transform parent, CancellationToken ct) =>
            (await assetsProvisioner.ProvideInstanceAsync(mapSettings.AtlasChunk, parent, ct: ct)).Value.GetComponent<SpriteRenderer>();

        private async UniTask<ParcelHighlightMarkerObject> GetParcelHighlightMarkerPrefabAsync(CancellationToken ct) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.ParcelHighlight, ct: ct)).Value.GetComponent<ParcelHighlightMarkerObject>();
    }
}
