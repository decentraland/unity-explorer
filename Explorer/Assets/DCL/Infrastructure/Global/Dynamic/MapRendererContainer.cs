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
using DCL.Web3.Identities;
using ECS;
using ECS.SceneLifeCycle.Realm;
using System;
using System.Threading;
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
                    eventBus));

                await mapRenderer.InitializeAsync(ct);
                c.MapRenderer = mapRenderer;
            });

            realmData.RealmType.OnUpdate += kind => mapRendererContainer.MapRenderer.SetSharedLayer(MapLayer.PlayerMarker, kind is RealmKind.GenesisCity);

            return mapRendererContainer;
        }

        protected override async UniTask InitializeInternalAsync(Settings settings, CancellationToken ct)
        {
            mapRendererSettings = await assetsProvisioner.ProvideMainAssetAsync(settings.MapRendererSettings, ct, nameof(settings.MapRendererSettings));
        }

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
