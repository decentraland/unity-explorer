using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.EventsApi;
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
using DCL.Web3.Identities;
using ECS;
using ECS.SceneLifeCycle.Realm;
using System;
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

            realmData.RealmType.OnUpdate += kind => ShowRealmMap(mapRendererContainer.MapRenderer, realmData, kind);
            ShowRealmMap(mapRendererContainer.MapRenderer, realmData, realmData.RealmType.Value);

            return mapRendererContainer;
        }

        /// <summary>
        ///     A world shows its own map, Genesis City and local scenes show Genesis City's. Local scenes keep the player marker hidden.
        /// </summary>
        private static void ShowRealmMap(IMapRenderer mapRenderer, IRealmData realmData, RealmKind kind)
        {
            switch (kind)
            {
                case RealmKind.World:
                    mapRenderer.ShowWorld(realmData.RealmName, WorldParcelBounds(realmData.WorldManifest));
                    break;
                case RealmKind.GenesisCity or RealmKind.LocalScene:
                    mapRenderer.ShowGenesisCity();
                    break;

                // Between realms: the map stays as it is until the next one is configured.
                default: return;
            }

            mapRenderer.SetSharedLayer(MapLayer.PlayerMarker, kind is not RealmKind.LocalScene);
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
