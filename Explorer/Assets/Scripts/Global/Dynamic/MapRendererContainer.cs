using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.MapRenderer;
using DCL.MapRenderer.ComponentsFactory;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.PlacesAPIService;
using DCL.PluginSystem;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using Utility.TeleportBus;

namespace Global.Dynamic
{
    public class MapRendererContainer : DCLWorldContainer<MapRendererContainer.Settings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        public MapRendererTextureContainer TextureContainer { get; }
        public IMapRenderer MapRenderer { get; private set; } = null!;
        private ProvidedAsset<MapRendererSettingsAsset> mapRendererSettings;

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
            IMapPathEventBus mapPathEventBus,
            INotificationsBusController notificationsBusController,
            ITeleportBusController teleportBusController,
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
                    mapPathEventBus,
                    teleportBusController,
                    notificationsBusController));

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
