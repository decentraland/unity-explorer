using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.MapPins.Bus;
using DCL.EventsApi;
using DCL.MapPins.Bus;
using DCL.MapRenderer.CoordsUtils;
using DCL.MapRenderer.Culling;
using DCL.MapRenderer.MapCameraController;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.Atlas;
using DCL.MapRenderer.MapLayers.Atlas.SatelliteAtlas;
using DCL.MapRenderer.MapLayers.Categories;
using DCL.MapRenderer.MapLayers.Cluster;
using DCL.MapRenderer.MapLayers.ParcelHighlight;
using DCL.MapRenderer.MapLayers.Pins;
using DCL.MapRenderer.MapLayers.SatelliteAtlas;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connectivity;
using DCL.Navmap;
using DCL.PlacesAPIService;
using DCL.WebRequests;
using ECS.SceneLifeCycle.Realm;
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
        private readonly IEventsApiService eventsApiService;
        private readonly IMapRendererSettings mapSettings;
        private readonly IMapPathEventBus mapPathEventBus;
        private readonly IMapPinsEventBus mapPinsEventBus;
        private readonly IRealmNavigator realmNavigator;
        private readonly INavmapBus navmapBus;
        private readonly IOnlineUsersProvider onlineUsersProvider;
        private PlayerMarkerInstaller playerMarkerInstaller { get; }
        private SceneOfInterestsMarkersInstaller sceneOfInterestMarkerInstaller { get; }
        private CategoryScenesMarkersInstaller categoriesMarkerInstaller { get; }
        private LiveEventsMarkersInstaller liveEventsMarkersInstaller { get; }
        private PinMarkerInstaller pinMarkerInstaller { get; }
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
            IMapPinsEventBus mapPinsEventBus,
            IRealmNavigator realmNavigator,
            INavmapBus navmapBus,
            IOnlineUsersProvider onlineUsersProvider)
        {
            this.assetsProvisioner = assetsProvisioner;
            mapSettings = settings;
            this.webRequestController = webRequestController;
            this.decentralandUrlsSource = decentralandUrlsSource;
            this.textureContainer = textureContainer;
            this.placesAPIService = placesAPIService;
            this.eventsApiService = eventsApiService;
            this.mapPathEventBus = mapPathEventBus;
            this.realmNavigator = realmNavigator;
            this.mapPinsEventBus = mapPinsEventBus;
            this.navmapBus = navmapBus;
            this.onlineUsersProvider = onlineUsersProvider;
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
            PinMarkerController pinMarkerController = await pinMarkerInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, cullingController, mapSettings, assetsProvisioner, mapPathEventBus, mapPinsEventBus, navmapBus, cancellationToken);

            ClusterMarkerObject? clusterPrefab = await GetClusterPrefabAsync(cancellationToken);
            ClusterMarkerObject? categoryMarkersClusterPrefab = await GetCategoryClusterPrefabAsync(cancellationToken);
            var clusterObjectsPool = new ObjectPool<ClusterMarkerObject>(
                () => CreateClusterPoolMethod(configuration, clusterPrefab, coordsUtils),
                actionOnGet: obj => obj.gameObject.SetActive(true),
                actionOnRelease: obj => obj.gameObject.SetActive(false));

            var searchResultsClusterObjectsPool = new ObjectPool<ClusterMarkerObject>(
                () => CreateSearchResultsClusterPoolMethod(configuration, categoryMarkersClusterPrefab, coordsUtils),
                actionOnGet: obj => obj.gameObject.SetActive(true),
                actionOnRelease: obj => obj.gameObject.SetActive(false));

            CategoryMarkerObject categoryMarkerPrefab = await GetCategoryMarkerPrefabAsync(cancellationToken);
            var liveEventsInstallTask = liveEventsMarkersInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, cullingController, mapSettings, eventsApiService, clusterObjectsPool, categoryMarkerPrefab, navmapBus, cancellationToken);
            var categoriesInstallerTask = categoriesMarkerInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, cullingController, mapSettings, clusterObjectsPool, categoryMarkerPrefab, navmapBus, cancellationToken);
            var sceneOfInterestInstallerTask = sceneOfInterestMarkerInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, cullingController, assetsProvisioner, mapSettings, placesAPIService, clusterObjectsPool, navmapBus, cancellationToken);
            var searchResultsInstallerTask = searchResultsMarkerInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, assetsProvisioner, mapSettings, cullingController, searchResultsClusterObjectsPool, navmapBus, cancellationToken);

            await UniTask.WhenAll(
                CreateParcelAtlasAsync(layers, configuration, coordsUtils, cullingController, cancellationToken),
                CreateSatelliteAtlasAsync(layers, configuration, coordsUtils, cullingController, cancellationToken),
                playerMarkerInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, cullingController, mapSettings, assetsProvisioner, mapPathEventBus, cancellationToken),
                hotUsersMarkersInstaller.InstallAsync(layers, configuration, coordsUtils, cullingController, assetsProvisioner, mapSettings, onlineUsersProvider, realmNavigator, cancellationToken),
                mapPathInstaller.InstallAsync(layers, zoomScalingLayers, configuration, coordsUtils, cullingController, mapSettings, assetsProvisioner, mapPathEventBus, cancellationToken)
                /* List of other creators that can be executed in parallel */);

            (IMapLayerController liveEventsInstaller,
                IMapLayerController categoriesInstaller,
                IMapLayerController sceneOfInterestInstaller,
                IMapLayerController searchResultsInstaller) = await UniTask.WhenAll(
                liveEventsInstallTask,
                categoriesInstallerTask,
                sceneOfInterestInstallerTask,
                searchResultsInstallerTask
            );

            List<IMapLayerController> interactableLayerControllers = new List<IMapLayerController>()
            {
                liveEventsInstaller,
                pinMarkerController,
                sceneOfInterestInstaller,
                categoriesInstaller,
                searchResultsInstaller
            };

            IObjectPool<IMapCameraControllerInternal> cameraControllersPool = new ObjectPool<IMapCameraControllerInternal>(
                () => CameraControllerBuilder(interactableLayerControllers),
                x => x.SetActive(true),
                x => x.SetActive(false),
                x => x.Dispose()
            );

            return new MapRendererComponents(configuration, layers, zoomScalingLayers, cullingController, cameraControllersPool);

            IMapCameraControllerInternal CameraControllerBuilder(List<IMapLayerController> interactableLayers)
            {
                MapCameraObject instance = Object.Instantiate(mapCameraObjectPrefab, configuration.MapCamerasRoot);
                var interactivityController = new MapCameraInteractivityController(configuration.MapCamerasRoot, instance.mapCamera, highlightMarkersPool, coordsUtils, interactableLayers, navmapBus, mapSettings.ClickAudio, mapSettings.HoverAudio);
                return new MapCameraController.MapCameraController(interactivityController, instance, coordsUtils, cullingController);
            }
        }

        private async UniTask<CategoryMarkerObject> GetCategoryMarkerPrefabAsync(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.CategoryMarker, cancellationToken)).Value;

        private static ClusterMarkerObject CreateClusterPoolMethod(MapRendererConfiguration configuration, ClusterMarkerObject prefab, ICoordsUtils coordsUtils)
        {
            ClusterMarkerObject clusterMarkerObject = Object.Instantiate(prefab, configuration.CategoriesMarkersRoot);

            for (var i = 0; i < clusterMarkerObject.renderers.Length; i++)
                clusterMarkerObject.renderers[i].sortingOrder = MapRendererDrawOrder.CATEGORIES;

            clusterMarkerObject.title.sortingOrder = MapRendererDrawOrder.CATEGORIES + 1;
            coordsUtils.SetObjectScale(clusterMarkerObject);
            return clusterMarkerObject;
        }

        private static ClusterMarkerObject CreateSearchResultsClusterPoolMethod(MapRendererConfiguration configuration, ClusterMarkerObject prefab, ICoordsUtils coordsUtils)
        {
            ClusterMarkerObject searchResultClusterObject = Object.Instantiate(prefab, configuration.SearchResultsMarkersRoot);

            for (var i = 0; i < searchResultClusterObject.renderers.Length; i++)
                searchResultClusterObject.renderers[i].sortingOrder = MapRendererDrawOrder.SEARCH_RESULTS;

            searchResultClusterObject.title.sortingOrder = MapRendererDrawOrder.SEARCH_RESULTS + 1;
            coordsUtils.SetObjectScale(searchResultClusterObject);
            return searchResultClusterObject;
        }

        private async UniTask<ClusterMarkerObject> GetClusterPrefabAsync(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.ClusterMarker, cancellationToken)).Value;

        private async UniTask<ClusterMarkerObject> GetCategoryClusterPrefabAsync(CancellationToken cancellationToken) =>
            (await assetsProvisioner.ProvideMainAssetAsync(mapSettings.SearchResultsClusterMarker, cancellationToken)).Value;

        private UniTask CreateParcelAtlasAsync(Dictionary<MapLayer, IMapLayerController> layers, MapRendererConfiguration configuration, ICoordsUtils coordsUtils, IMapCullingController cullingController, CancellationToken cancellationToken)
        {
            var chunkAtlas = new ParcelChunkAtlasController(configuration.AtlasRoot, IMapRendererSettings.ATLAS_CHUNK_SIZE, coordsUtils, cullingController, chunkBuilder: CreateChunkAsync);

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
                    coordsCenter
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

                var chunk = new SatelliteChunkController(atlasChunkPrefab, webRequestController, textureContainer, chunkLocalPosition, chunkId, MapRendererDrawOrder.SATELLITE_ATLAS);
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
