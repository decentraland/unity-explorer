using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.MapPins.Bus;
using DCL.MapRenderer;
using DCL.MapRenderer.ComponentsFactory;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Navmap;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.PlacesAPIService;
using DCL.PluginSystem;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Utility.TeleportBus;
using DCL.EventsApi;

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
            IEventsApiService eventsAPIService,
            IMapPathEventBus mapPathEventBus,
            IMapPinsEventBus mapPinsEventBus,
            INotificationsBusController notificationsBusController,
            ITeleportBusController teleportBusController,
            INavmapBus navmapBus,
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
                    notificationsBusController,
                    navmapBus));

                await mapRenderer.InitializeAsync(ct);
                c.MapRenderer = mapRenderer;
            });

            return mapRendererContainer;
        }

        protected override async UniTask InitializeInternalAsync(Settings settings, CancellationToken ct)
        {
            mapRendererSettings = await assetsProvisioner.ProvideMainAssetAsync(settings.MapRendererSettings, ct, nameof(settings.MapRendererSettings));
        }

        public override void Dispose()
        {
            base.Dispose();
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
