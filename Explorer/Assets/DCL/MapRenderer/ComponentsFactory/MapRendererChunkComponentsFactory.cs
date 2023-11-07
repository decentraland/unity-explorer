using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCLServices.MapRenderer.CoordsUtils;
using DCLServices.MapRenderer.Culling;
using DCLServices.MapRenderer.MapCameraController;
using DCLServices.MapRenderer.MapLayers;
using DCLServices.MapRenderer.MapLayers.Atlas;
using DCLServices.MapRenderer.MapLayers.Atlas.SatelliteAtlas;
using DCLServices.MapRenderer.MapLayers.ParcelHighlight;
using DCLServices.MapRenderer.MapLayers.SatelliteAtlas;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;

namespace DCLServices.MapRenderer.ComponentsFactory
{
    public class MapRendererChunkComponentsFactory : IMapRendererComponentsFactory
    {
        private const int ATLAS_CHUNK_SIZE = 1020;
        private const int PARCEL_SIZE = 20;
        // it is quite expensive to disable TextMeshPro so larger bounds should help keeping the right balance
        private const float CULLING_BOUNDS_IN_PARCELS = 10;

        private const string ATLAS_CHUNK_ADDRESS = "AtlasChunk";
        private const string MAP_CONFIGURATION_ADDRESS = "MapRendererConfiguration";
        private const string MAP_CAMERA_OBJECT_ADDRESS = "MapCameraObject";
        private const string PARCEL_HIGHLIGHT_OBJECT_ADDRESS = "MapParcelHighlightMarker";

        internal PlayerMarkerInstaller playerMarkerInstaller { get; }

        private IAssetsProvisioner assetsProvisioner;
        private MapRendererSettings mapSettings;

        public MapRendererChunkComponentsFactory (IAssetsProvisioner assetsProvisioner, MapRendererSettings settings)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mapSettings = settings;
        }

        async UniTask<MapRendererComponents> IMapRendererComponentsFactory.Create(CancellationToken cancellationToken)
        {
            this.mapSettings = mapSettings;
            MapRendererConfiguration configuration = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(mapSettings.MapRendererConfiguration, ct: CancellationToken.None)).Value.GetComponent<MapRendererConfiguration>());
            var coordsUtils = new ChunkCoordsUtils(PARCEL_SIZE);
            IMapCullingController cullingController = new MapCullingController(new MapCullingRectVisibilityChecker(CULLING_BOUNDS_IN_PARCELS * PARCEL_SIZE));
            var layers = new Dictionary<MapLayer, IMapLayerController>();
            var zoomScalingLayers = new List<IZoomScalingLayer>();

            ParcelHighlightMarkerObject highlightMarkerPrefab = await GetParcelHighlightMarkerPrefab(cancellationToken);
            var highlightMarkersPool = new ObjectPool<IParcelHighlightMarker>(
                () => CreateHighlightMarker(highlightMarkerPrefab, configuration, coordsUtils),
                _ => { },
                marker => marker.Deactivate(),
                marker => marker.Dispose(),
                defaultCapacity: 1
            );

            MapCameraObject mapCameraObjectPrefab = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(mapSettings.MapCameraObject, ct: CancellationToken.None)).Value.GetComponent<MapCameraObject>());
            IObjectPool<IMapCameraControllerInternal> cameraControllersPool = new ObjectPool<IMapCameraControllerInternal>(
                CameraControllerBuilder,
                x => x.SetActive(true),
                x => x.SetActive(false),
                x => x.Dispose()
            );

            await UniTask.WhenAll(
                CreateAtlas(layers, configuration, coordsUtils, cullingController, cancellationToken),
                CreateSatelliteAtlas(layers, configuration, coordsUtils, cullingController, cancellationToken),
                playerMarkerInstaller.Install(layers, zoomScalingLayers, configuration, coordsUtils, cullingController, mapSettings, cancellationToken)
                /* List of other creators that can be executed in parallel */);

            return new MapRendererComponents(configuration, layers, zoomScalingLayers, cullingController, cameraControllersPool);

            IMapCameraControllerInternal CameraControllerBuilder()
            {
                MapCameraObject instance = Object.Instantiate(mapCameraObjectPrefab, configuration.MapCamerasRoot);
                var interactivityController = new MapCameraInteractivityController(configuration.MapCamerasRoot, instance.mapCamera, highlightMarkersPool, coordsUtils);

                return new MapCameraController.MapCameraController(interactivityController, instance, coordsUtils, cullingController);
            }
        }

        private async UniTask CreateSatelliteAtlas(Dictionary<MapLayer, IMapLayerController> layers, MapRendererConfiguration configuration, ICoordsUtils coordsUtils, IMapCullingController cullingController, CancellationToken cancellationToken)
        {
            const int GRID_SIZE = 8; // satellite images are provided by 8x8 grid.
            const int PARCELS_INSIDE_CHUNK = 40; // One satellite image contains 40 parcels.

            var chunkAtlas = new SatelliteChunkAtlasController(configuration.SatelliteAtlasRoot, GRID_SIZE, PARCELS_INSIDE_CHUNK, coordsUtils, cullingController, chunkBuilder: CreateSatelliteChunk);

            // initialize Atlas but don't block the flow (to accelerate loading time)
            chunkAtlas.Initialize(cancellationToken).SuppressCancellationThrow().Forget();

            layers.Add(MapLayer.SatelliteAtlas, chunkAtlas);
            return;

            async UniTask<IChunkController> CreateSatelliteChunk(Vector3 chunkLocalPosition, Vector2Int chunkId, Transform parent, CancellationToken ct)
            {
                SpriteRenderer atlasChunkPrefab = await GetAtlasChunkPrefab(ct);

                var chunk = new SatelliteChunkController(atlasChunkPrefab, chunkLocalPosition, chunkId, parent, MapRendererDrawOrder.SATELLITE_ATLAS);
                await chunk.LoadImage(chunkId, PARCELS_INSIDE_CHUNK * coordsUtils.ParcelSize, ct);

                return chunk;
            }
        }

        private async UniTask CreateAtlas(Dictionary<MapLayer, IMapLayerController> layers, MapRendererConfiguration configuration, ICoordsUtils coordsUtils, IMapCullingController cullingController, CancellationToken cancellationToken)
        {
            var chunkAtlas = new ParcelChunkAtlasController(configuration.AtlasRoot, ATLAS_CHUNK_SIZE, coordsUtils, cullingController, chunkBuilder: CreateChunk);

            // initialize Atlas but don't block the flow (to accelerate loading time)
            chunkAtlas.Initialize(cancellationToken).SuppressCancellationThrow().Forget();

            layers.Add(MapLayer.ParcelsAtlas, chunkAtlas);
            return;

            async UniTask<IChunkController> CreateChunk(Vector3 chunkLocalPosition, Vector2Int coordsCenter, Transform parent, CancellationToken ct)
            {
                SpriteRenderer atlasChunkPrefab = await GetAtlasChunkPrefab(ct);

                var chunk = new ParcelChunkController(atlasChunkPrefab, chunkLocalPosition, coordsCenter, parent);
                chunk.SetDrawOrder(MapRendererDrawOrder.ATLAS);
                await chunk.LoadImage(ATLAS_CHUNK_SIZE, PARCEL_SIZE, coordsCenter, ct);

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

        internal async Task<SpriteRenderer> GetAtlasChunkPrefab(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.MapCameraObject, ct: CancellationToken.None)).Value.GetComponent<SpriteRenderer>();

        private async UniTask<ParcelHighlightMarkerObject> GetParcelHighlightMarkerPrefab(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.MapCameraObject, ct: CancellationToken.None)).Value.GetComponent<ParcelHighlightMarkerObject>();
    }
}
