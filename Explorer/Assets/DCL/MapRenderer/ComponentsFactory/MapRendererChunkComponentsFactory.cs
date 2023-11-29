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
        private const int ATLAS_CHUNK_SIZE = 1020;
        private const int PARCEL_SIZE = 20;

        // it is quite expensive to disable TextMeshPro so larger bounds should help keeping the right balance
        private const float CULLING_BOUNDS_IN_PARCELS = 10;

        internal PlayerMarkerInstaller playerMarkerInstaller { get; }

        private readonly IAssetsProvisioner assetsProvisioner;

        private readonly IWebRequestController webRequestController;
        private readonly MapRendererSettings mapSettings;

        public MapRendererChunkComponentsFactory(IAssetsProvisioner assetsProvisioner, MapRendererSettings settings, IWebRequestController webRequestController)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mapSettings = settings;
            this.webRequestController = webRequestController;
        }

        async UniTask<MapRendererComponents> IMapRendererComponentsFactory.CreateAsync(CancellationToken cancellationToken)
        {
            MapRendererConfiguration configuration = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(mapSettings.MapRendererConfiguration, ct: CancellationToken.None)).Value.GetComponent<MapRendererConfiguration>());
            var coordsUtils = new ChunkCoordsUtils(PARCEL_SIZE);
            IMapCullingController cullingController = new MapCullingController(new MapCullingRectVisibilityChecker(CULLING_BOUNDS_IN_PARCELS * PARCEL_SIZE));
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
                playerMarkerInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, cullingController, mapSettings, assetsProvisioner, cancellationToken)
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

            // initialize Atlas but don't block the flow (to accelerate loading time)
            chunkAtlas.InitializeAsync(cancellationToken).SuppressCancellationThrow().Forget();

            layers.Add(MapLayer.SatelliteAtlas, chunkAtlas);
            return;

            async UniTask<IChunkController> CreateSatelliteChunkAsync(Vector3 chunkLocalPosition, Vector2Int chunkId, Transform parent, CancellationToken ct)
            {
                SpriteRenderer atlasChunkPrefab = await GetAtlasChunkPrefabAsync(ct);

                var chunk = new SatelliteChunkController(atlasChunkPrefab, webRequestController, chunkLocalPosition, chunkId, parent, MapRendererDrawOrder.SATELLITE_ATLAS);
                await chunk.LoadImageAsync(chunkId, PARCELS_INSIDE_CHUNK * coordsUtils.ParcelSize, ct);

                return chunk;
            }
        }

        private async UniTask CreateAtlasAsync(Dictionary<MapLayer, IMapLayerController> layers, MapRendererConfiguration configuration, ICoordsUtils coordsUtils, IMapCullingController cullingController, CancellationToken cancellationToken)
        {
            var chunkAtlas = new ParcelChunkAtlasController(configuration.AtlasRoot, ATLAS_CHUNK_SIZE, coordsUtils, cullingController, chunkBuilder: CreateChunkAsync);

            // initialize Atlas but don't block the flow (to accelerate loading time)
            chunkAtlas.InitializeAsync(cancellationToken).SuppressCancellationThrow().Forget();

            layers.Add(MapLayer.ParcelsAtlas, chunkAtlas);
            return;

            async UniTask<IChunkController> CreateChunkAsync(Vector3 chunkLocalPosition, Vector2Int coordsCenter, Transform parent, CancellationToken ct)
            {
                SpriteRenderer atlasChunkPrefab = await GetAtlasChunkPrefabAsync(ct);

                var chunk = new ParcelChunkController(webRequestController, atlasChunkPrefab, chunkLocalPosition, coordsCenter, parent);
                chunk.SetDrawOrder(MapRendererDrawOrder.ATLAS);
                await chunk.LoadImageAsync(ATLAS_CHUNK_SIZE, PARCEL_SIZE, coordsCenter, ct);

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

        internal async Task<SpriteRenderer> GetAtlasChunkPrefabAsync(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideInstanceAsync(mapSettings.AtlasChunk, ct: CancellationToken.None)).Value.GetComponent<SpriteRenderer>();

        private async UniTask<ParcelHighlightMarkerObject> GetParcelHighlightMarkerPrefabAsync(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.ParcelHighlight, ct: CancellationToken.None)).Value.GetComponent<ParcelHighlightMarkerObject>();
    }
}
