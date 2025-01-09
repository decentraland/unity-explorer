﻿using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.MapPins.Bus;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Atlas;
using DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas;
using DCL.MapRenderer.MapLayers.ParcelHighlight;
using DCL.MapRenderer.MapLayers.Pins;
using DCL.MapRenderer.MapLayers.SatelliteAtlas;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.PlacesAPIService;
using DCL.WebRequests;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;
using Object = UnityEngine.Object;

namespace DCL.MapRenderer.ComponentsFactory
{
    public class MapRendererChunkComponentsFactory : IMapRendererComponentsFactory
    {
        private readonly IAssetsProvisioner assetsProvisioner;

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly MapRendererTextureContainer textureContainer;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IMapRendererSettings mapSettings;
        private readonly IMapPathEventBus mapPathEventBus;
        private readonly IMapPinsEventBus mapPinsEventBus;
        private readonly INotificationsBusController notificationsBusController;
        private PlayerMarkerInstaller playerMarkerInstaller { get; }
        private SceneOfInterestsMarkersInstaller sceneOfInterestMarkerInstaller { get; }
        private PinMarkerInstaller pinMarkerInstaller { get; }
        private FavoritesMarkersInstaller favoritesMarkersInstaller { get; }
        private HotUsersMarkersInstaller hotUsersMarkersInstaller { get; }
        private MapPathInstaller mapPathInstaller { get; }

        public MapRendererChunkComponentsFactory(
            IAssetsProvisioner assetsProvisioner,
            IMapRendererSettings settings,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrlsSource,
            MapRendererTextureContainer textureContainer,
            IPlacesAPIService placesAPIService,
            IMapPathEventBus mapPathEventBus,
            IMapPinsEventBus mapPinsEventBus,
            INotificationsBusController notificationsBusController)
        {
            this.assetsProvisioner = assetsProvisioner;
            mapSettings = settings;
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.textureContainer = textureContainer;
            this.placesAPIService = placesAPIService;
            this.mapPathEventBus = mapPathEventBus;
            this.notificationsBusController = notificationsBusController;
            this.mapPinsEventBus = mapPinsEventBus;
        }

        async UniTask<MapRendererComponents> IMapRendererComponentsFactory.CreateAsync(CancellationToken cancellationToken)
        {
            MapRendererConfiguration configuration = Object.Instantiate((await assetsProvisioner.ProvideMainAssetAsync(mapSettings.MapRendererConfiguration, ct: cancellationToken)).Value);
            var coordsUtils = new ChunkCoordsUtils(IMapRendererSettings.PARCEL_SIZE);
            IMapCullingController cullingController = new MapCullingController(new MapCullingRectVisibilityChecker(IMapRendererSettings.CULLING_BOUNDS_IN_PARCELS * IMapRendererSettings.PARCEL_SIZE));
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

            MapCameraObject mapCameraObjectPrefab = (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.MapCameraObject, ct: cancellationToken)).Value;
            PinMarkerController pinMarkerController = await pinMarkerInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, cullingController, mapSettings, assetsProvisioner, mapPathEventBus, mapPinsEventBus, cancellationToken);

            IObjectPool<IMapCameraControllerInternal> cameraControllersPool = new ObjectPool<IMapCameraControllerInternal>(
                CameraControllerBuilder,
                x => x.SetActive(true),
                x => x.SetActive(false),
                x => x.Dispose()
            );

            await UniTask.WhenAll(
                CreateSatelliteAtlasAsync(layers, configuration, coordsUtils, cullingController, cancellationToken),
                playerMarkerInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, cullingController, mapSettings, assetsProvisioner, mapPathEventBus, cancellationToken),
                sceneOfInterestMarkerInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, cullingController, assetsProvisioner, mapSettings, placesAPIService, cancellationToken),
                favoritesMarkersInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, cullingController, placesAPIService, assetsProvisioner, mapSettings, cancellationToken),
                hotUsersMarkersInstaller.InstallAsync(layers, configuration, coordsUtils, cullingController, assetsProvisioner, mapSettings, cancellationToken),
                mapPathInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, cullingController, mapSettings, assetsProvisioner, mapPathEventBus, notificationsBusController, cancellationToken)
                /* List of other creators that can be executed in parallel */);

            return new MapRendererComponents(configuration, layers, zoomScalingLayers, cullingController, cameraControllersPool);

            IMapCameraControllerInternal CameraControllerBuilder()
            {
                MapCameraObject instance = Object.Instantiate(mapCameraObjectPrefab, configuration.MapCamerasRoot);
                var interactivityController = new MapCameraInteractivityController(configuration.MapCamerasRoot, instance.mapCamera, highlightMarkersPool, coordsUtils, pinMarkerController);

                return new MapCameraController.MapCameraController(interactivityController, instance, coordsUtils, cullingController);
            }
        }

        private UniTask CreateSatelliteAtlasAsync(Dictionary<MapLayer, IMapLayerController> layers, MapRendererConfiguration configuration, ICoordsUtils coordsUtils, IMapCullingController cullingController, CancellationToken cancellationToken)
        {
            const int GRID_SIZE = 8; // satellite images are provided by 8x8 grid.
            const int PARCELS_INSIDE_CHUNK = 40; // One satellite image contains 40 parcels.

            var chunkAtlas = new SatelliteChunkAtlasController(configuration.SatelliteAtlasRoot, GRID_SIZE, PARCELS_INSIDE_CHUNK, coordsUtils, cullingController, chunkBuilder: CreateSatelliteChunkAsync);

            chunkAtlas.InitializeAsync(cancellationToken).SuppressCancellationThrow().Forget();

            layers.Add(MapLayer.SatelliteAtlas, chunkAtlas);
            return UniTask.CompletedTask;

            async UniTask<IChunkController> CreateSatelliteChunkAsync(Vector3 chunkLocalPosition, Vector2Int chunkId, Transform parent, CancellationToken ct)
            {
                SpriteRenderer atlasChunkPrefab = await GetAtlasChunkPrefabAsync(parent, ct);

                var chunk = new SatelliteChunkController(atlasChunkPrefab, webRequestController, textureContainer, chunkLocalPosition, chunkId, parent, MapRendererDrawOrder.SATELLITE_ATLAS);
                await chunk.LoadImageAsync(chunkId, PARCELS_INSIDE_CHUNK * coordsUtils.ParcelSize, ct);

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
            (await assetsProvisioner.ProvideInstanceAsync(mapSettings.AtlasChunk, parent, ct: ct)).Value;

        private async UniTask<ParcelHighlightMarkerObject> GetParcelHighlightMarkerPrefabAsync(CancellationToken ct) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.ParcelHighlight, ct: ct)).Value;
    }
}
