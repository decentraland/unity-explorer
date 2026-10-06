using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Diagnostics;
using DCL.EventsApi;
using DCL.Ipfs;
using DCL.MapPins.Bus;
using DCL.MapRenderer;
using DCL.MapRenderer.ComponentsFactory;
using DCL.MapRenderer.MapLayers;
using DCL.MapRenderer.MapLayers.HomeMarker;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Connectivity;
using DCL.Navmap;
using DCL.PlacesAPIService;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.RealmNavigation;
using DCL.Utilities.Extensions;
using DCL.Utility.Types;
using DCL.Web3.Identities;
using ECS;
using ECS.SceneLifeCycle.Realm;
using System;
using System.Collections.Generic;
using System.Threading;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Utility;

namespace Global.Dynamic
{
    public class MapRendererContainer : DCLWorldContainer<MapRendererContainer.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private ProvidedAsset<MapRendererSettingsAsset> mapRendererSettings;
        private IRealmData realmData = null!;
        private IGlobalRealmController realmController = null!;
        private CancellationTokenSource? worldParcelsCts;
        public MapRendererTextureContainer TextureContainer { get; }
        public IMapRenderer MapRenderer { get; private set; } = null!;

        private MapRendererContainer(IAssetsProvisioner assetsProvisioner, MapRendererTextureContainer textureContainer)
        {
            this.assetsProvisioner = assetsProvisioner;
            TextureContainer = textureContainer;
        }

        public static async UniTask<MapRendererContainer> CreateAsync(
            IPluginSettingsContainer settingsContainer,
            StaticContainer staticContainer,
            IDecentralandUrlsSource decentralandUrlsSource,
            IAssetsProvisioner assetsProvisioner,
            IPlacesAPIService placesAPIService,
            HttpEventsApiService eventsAPIService,
            IMapPathEventBus mapPathEventBus,
            IMapPinsEventBus mapPinsEventBus,
            IRealmNavigator teleportBusController,
            IRealmData realmData,
            IGlobalRealmController realmController,
            INavmapBus navmapBus,
            IOnlineUsersProvider onlineUsersProvider,
            IWeb3IdentityCache web3IdentityCache,
            HomePlaceEventBus homePlaceEventBus,
            IEventBus eventBus,
            string? satelliteDetailTilesUrl,
            CancellationToken ct)
        {
            var mapRendererContainer = new MapRendererContainer(assetsProvisioner, new MapRendererTextureContainer());

            await mapRendererContainer.InitializeContainerAsync<MapRendererContainer, Settings>(settingsContainer, ct, async c =>
            {
                var mapRenderer = new MapRenderer(new MapRendererChunkComponentsFactory(
                    assetsProvisioner,
                    c.mapRendererSettings.Value,
                    staticContainer.WebRequestsContainer.WebRequestController,
                    decentralandUrlsSource,
                    c.TextureContainer,
                    placesAPIService,
                    eventsAPIService,
                    mapPathEventBus,
                    mapPinsEventBus,
                    teleportBusController,
                    navmapBus,
                    onlineUsersProvider,
                    web3IdentityCache,
                    homePlaceEventBus,
                    eventBus,
                    satelliteDetailTilesUrl,
                    staticContainer.BytesDiskCache));

                await mapRenderer.InitializeAsync(ct);
                c.MapRenderer = mapRenderer;
            });

            mapRendererContainer.realmData = realmData;
            mapRendererContainer.realmController = realmController;
            realmData.RealmType.OnUpdate += mapRendererContainer.ShowRealmMap;
            mapRendererContainer.ShowRealmMap(realmData.RealmType.Value);

            return mapRendererContainer;
        }

        public override void Dispose()
        {
            realmData.RealmType.OnUpdate -= ShowRealmMap;
            worldParcelsCts.SafeCancelAndDispose();
        }

        /// <summary>
        ///     A world shows its own map, Genesis City and local scenes show Genesis City's. Local scenes keep the player marker hidden.
        /// </summary>
        private void ShowRealmMap(RealmKind kind)
        {
            worldParcelsCts = worldParcelsCts.SafeRestart();

            switch (kind)
            {
                case RealmKind.World:
                    RectInt? parcels = WorldParcelBounds(realmData.WorldManifest);
                    MapRenderer.ShowWorld(realmData.RealmName, parcels);

                    // A world the registry hasn't indexed has no manifest: its parcels are known once its scenes are.
                    if (parcels == null)
                        ShowWorldOfFixedScenesAsync(realmData.RealmName, worldParcelsCts.Token).Forget();

                    break;
                case RealmKind.GenesisCity or RealmKind.LocalScene:
                    MapRenderer.ShowGenesisCity();
                    break;

                // Between realms: the map stays as it is until the next one is configured.
                default: return;
            }

            MapRenderer.SetSharedLayer(MapLayer.PlayerMarker, kind is not RealmKind.LocalScene);
        }

        private async UniTaskVoid ShowWorldOfFixedScenesAsync(string worldName, CancellationToken ct)
        {
            Result<List<SceneEntityDefinition>> scenes = await realmController.WaitForFixedScenePromisesAsync(ct)
                                                                              .SuppressToResultAsync(ReportCategory.UI);

            if (ct.IsCancellationRequested || !scenes.Success)
                return;

            if (TryGetParcelBounds(scenes.Value, out RectInt parcels))
                MapRenderer.ShowWorld(worldName, parcels);
        }

        private static bool TryGetParcelBounds(List<SceneEntityDefinition> scenes, out RectInt bounds)
        {
            var min = new Vector2Int(int.MaxValue, int.MaxValue);
            var max = new Vector2Int(int.MinValue, int.MinValue);

            foreach (SceneEntityDefinition scene in scenes)
            foreach (Vector2Int parcel in scene.metadata.scene.DecodedParcels)
            {
                min = Vector2Int.Min(min, parcel);
                max = Vector2Int.Max(max, parcel);
            }

            bounds = new RectInt(min, max - min + Vector2Int.one);
            return min.x <= max.x;
        }

        private static RectInt? WorldParcelBounds(WorldManifest manifest) =>
            manifest.TryGetOccupiedBounds(out int2 min, out int2 max)
                ? new RectInt(min.x, min.y, max.x - min.x + 1, max.y - min.y + 1)
                : null;

        public MapRendererPlugin CreatePlugin() =>
            new (MapRenderer);

        protected override async UniTask InitializeInternalAsync(Settings settings, CancellationToken ct)
        {
            mapRendererSettings = await assetsProvisioner.ProvideMainAssetAsync(settings.MapRendererSettings, ct, nameof(settings.MapRendererSettings));
        }

        [Serializable]
        public class Settings : IDCLPluginSettings
        {
            [field: SerializeField] public MapRendererSettingsRef MapRendererSettings { get; private set; } = null!;

            [Serializable]
            public class MapRendererSettingsRef : AssetReferenceT<MapRendererSettingsAsset>
            {
                public MapRendererSettingsRef(string guid) : base(guid) { }
            }
        }
    }
}
