using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.EventsApi;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Atlas;
using DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas;
using DCL.MapRenderer.MapLayers.Categories;
using DCL.MapRenderer.MapLayers.ParcelHighlight;
using DCL.MapRenderer.MapLayers.Pins;
using DCL.MapRenderer.MapLayers.SatelliteAtlas;
using DCL.MapRenderer.MapLayers.UsersMarker;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Navmap;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.PlacesAPIService;
using DCL.WebRequests;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Pool;
using Utility.TeleportBus;
using Object = UnityEngine.Object;

namespace DCL.MapRenderer.ComponentsFactory
{
    public class MapRendererChunkComponentsFactory : IMapRendererComponentsFactory
    {
        private const int PREWARM_COUNT = 60;
        private readonly IAssetsProvisioner assetsProvisioner;

        private readonly IWebRequestController webRequestController;
        private readonly IDecentralandUrlsSource decentralandUrlsSource;
        private readonly MapRendererTextureContainer textureContainer;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IEventsApiService eventsApiService;
        private readonly IMapRendererSettings mapSettings;
        private readonly IMapPathEventBus mapPathEventBus;
        private readonly ITeleportBusController teleportBusController;
        private readonly INotificationsBusController notificationsBusController;
        private readonly INavmapBus navmapBus;
        private PlayerMarkerInstaller playerMarkerInstaller { get; }
        private SceneOfInterestsMarkersInstaller sceneOfInterestMarkerInstaller { get; }
        private CategoryScenesMarkersInstaller categoriesMarkerInstaller { get; }
        private LiveEventsMarkersInstaller liveEventsMarkersInstaller { get; }
        private PinMarkerInstaller pinMarkerInstaller { get; }
        private FavoritesMarkersInstaller favoritesMarkersInstaller { get; }
        private HotUsersMarkersInstaller hotUsersMarkersInstaller { get; }
        private SearchResultsMarkersInstaller searchResultsMarkerInstaller { get; }
        private MapPathInstaller mapPathInstaller { get; }

        public MapRendererChunkComponentsFactory(
            IAssetsProvisioner assetsProvisioner,
            IMapRendererSettings settings,
            IWebRequestController webRequestController,
            IDecentralandUrlsSource decentralandUrlsSource,
            MapRendererTextureContainer textureContainer,
            IPlacesAPIService placesAPIService,
            IEventsApiService eventsApiService,
            IMapPathEventBus mapPathEventBus,
            ITeleportBusController teleportBusController,
            INotificationsBusController notificationsBusController,
            INavmapBus navmapBus)
        {
            this.assetsProvisioner = assetsProvisioner;
            mapSettings = settings;
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.textureContainer = textureContainer;
            this.placesAPIService = placesAPIService;
            this.eventsApiService = eventsApiService;
            this.mapPathEventBus = mapPathEventBus;
            this.teleportBusController = teleportBusController;
            this.notificationsBusController = notificationsBusController;
            this.navmapBus = navmapBus;
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
            PinMarkerController pinMarkerController = await pinMarkerInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, cullingController, mapSettings, assetsProvisioner, mapPathEventBus, cancellationToken);
            RemoteUsersRequestController remoteUsersRequestController = new RemoteUsersRequestController(webRequestController, decentralandUrlsSource);

            IObjectPool<IMapCameraControllerInternal> cameraControllersPool = new ObjectPool<IMapCameraControllerInternal>(
                CameraControllerBuilder,
                x => x.SetActive(true),
                x => x.SetActive(false),
                x => x.Dispose()
            );
            ClusterMarkerObject? clusterPrefab = await GetClusterPrefabAsync(cancellationToken);
            var clusterObjectsPool = new ObjectPool<ClusterMarkerObject>(
                () => CreateClusterPoolMethod(configuration, clusterPrefab, coordsUtils),
                defaultCapacity: PREWARM_COUNT,
                actionOnGet: obj => obj.gameObject.SetActive(true),
                actionOnRelease: obj => obj.gameObject.SetActive(false));

            CategoryMarkerObject categoryMarkerPrefab = await GetCategoryMarkerPrefabAsync(cancellationToken);

            await UniTask.WhenAll(
                CreateParcelAtlasAsync(layers, configuration, coordsUtils, cullingController, cancellationToken),
                CreateSatelliteAtlasAsync(layers, configuration, coordsUtils, cullingController, cancellationToken),
                playerMarkerInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, cullingController, mapSettings, assetsProvisioner, mapPathEventBus, cancellationToken),
                sceneOfInterestMarkerInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, cullingController, assetsProvisioner, mapSettings, placesAPIService, clusterObjectsPool, cancellationToken),
                categoriesMarkerInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, cullingController, assetsProvisioner, mapSettings, placesAPIService, clusterObjectsPool, categoryMarkerPrefab, cancellationToken),
                liveEventsMarkersInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, cullingController, assetsProvisioner, mapSettings, eventsApiService, clusterObjectsPool, categoryMarkerPrefab, cancellationToken),
                favoritesMarkersInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, cullingController, placesAPIService, assetsProvisioner, mapSettings, clusterObjectsPool, cancellationToken),
                hotUsersMarkersInstaller.InstallAsync(layers, configuration, coordsUtils, cullingController, assetsProvisioner, mapSettings, teleportBusController, remoteUsersRequestController, cancellationToken),
                searchResultsMarkerInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, assetsProvisioner, mapSettings, cullingController, navmapBus, cancellationToken),
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

        private async UniTask<CategoryMarkerObject> GetCategoryMarkerPrefabAsync(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.CategoryMarker, cancellationToken)).Value;

        private static IClusterMarker CreateClusterMarker(IObjectPool<ClusterMarkerObject> objectsPool, IMapCullingController cullingController, ICoordsUtils coordsUtils) =>
            new ClusterMarker(objectsPool, cullingController, coordsUtils);

        private static ClusterMarkerObject CreateClusterPoolMethod(MapRendererConfiguration configuration, ClusterMarkerObject prefab, ICoordsUtils coordsUtils)
        {
            ClusterMarkerObject categoryMarkerObject = Object.Instantiate(prefab, configuration.CategoriesMarkersRoot);

            for (var i = 0; i < categoryMarkerObject.renderers.Length; i++)
                categoryMarkerObject.renderers[i].sortingOrder = MapRendererDrawOrder.CATEGORIES;

            categoryMarkerObject.title.sortingOrder = MapRendererDrawOrder.CATEGORIES + 1;
            coordsUtils.SetObjectScale(categoryMarkerObject);
            return categoryMarkerObject;
        }

        private async UniTask<ClusterMarkerObject> GetClusterPrefabAsync(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.ClusterMarker, cancellationToken)).Value;

        private UniTask CreateParcelAtlasAsync(Dictionary<MapLayer, IMapLayerController> layers, MapRendererConfiguration configuration, ICoordsUtils coordsUtils, IMapCullingController cullingController, CancellationToken cancellationToken)
        {
            var chunkAtlas = new ParcelChunkAtlasController(configuration.AtlasRoot, IMapRendererSettings.ATLAS_CHUNK_SIZE, coordsUtils, cullingController, chunkBuilder: CreateChunkAsync);

            // initialize Atlas but don't block the flow (to accelerate loading time)
            chunkAtlas.InitializeAsync(cancellationToken).SuppressCancellationThrow().Forget();

            layers.Add(MapLayer.ParcelsAtlas, chunkAtlas);
            return UniTask.CompletedTask;

            async UniTask<IChunkController> CreateChunkAsync(Vector3 chunkLocalPosition, Vector2Int coordsCenter, Transform parent, CancellationToken ct)
            {
                SpriteRenderer atlasChunkPrefab = await GetAtlasChunkPrefabAsync(parent, ct);

                var chunk = new ParcelChunkController(
                    webRequestController,
                    decentralandUrlsSource,
                    atlasChunkPrefab,
                    chunkLocalPosition,
                    coordsCenter,
                    parent
                );

                chunk.SetDrawOrder(MapRendererDrawOrder.ATLAS);

                // If it takes more than CHUNKS_MAX_WAIT_TIME to load the chunk, it will be finished asynchronously
                await chunk.LoadImageAsync(IMapRendererSettings.ATLAS_CHUNK_SIZE, IMapRendererSettings.PARCEL_SIZE, coordsCenter, ct);

                return chunk;
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
