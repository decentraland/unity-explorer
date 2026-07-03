using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.ThirdParty;
using DCL.Backpack.BackpackBus;
using DCL.DebugUtilities;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Multiplayer.Emotes;
using DCL.PluginSystem.Global;
using DCL.Web3.Identities;
using Global.AppArgs;
using Utility;

namespace Global.Dynamic
{
    /// <summary>
    ///     Wearable and emote catalogs and the providers that resolve them from the realm, ECS or builder API.
    /// </summary>
    public class WearableContainer
    {
        private readonly TrimmedWearableStorage trimmedWearableCatalog;

        public WearableStorage WearableCatalog { get; }

        public TrimmedEmoteStorage TrimmedEmoteCatalog { get; }

        public IEmoteProvider EmoteProvider { get; }

        public ApplicationParametersWearablesProvider WearablesProvider { get; }

        public IThirdPartyNftProviderSource ThirdPartyNftProviderSource { get; }

        public URLDomain BuilderContentURL { get; }

        public IEventBus EmotesEventBus { get; }

        public EmoteWheelShortcutHandler EmoteWheelShortcutHandler { get; }

        public IBackpackEventBus BackpackEventBus { get; }

        public ECSThumbnailProvider ThumbnailProvider { get; }

        private WearableContainer(
            WearableStorage wearableCatalog,
            TrimmedWearableStorage trimmedWearableCatalog,
            TrimmedEmoteStorage trimmedEmoteCatalog,
            IEmoteProvider emoteProvider,
            ApplicationParametersWearablesProvider wearablesProvider,
            IThirdPartyNftProviderSource thirdPartyNftProviderSource,
            URLDomain builderContentURL,
            IEventBus emotesEventBus,
            EmoteWheelShortcutHandler emoteWheelShortcutHandler,
            IBackpackEventBus backpackEventBus,
            ECSThumbnailProvider thumbnailProvider)
        {
            WearableCatalog = wearableCatalog;
            this.trimmedWearableCatalog = trimmedWearableCatalog;
            TrimmedEmoteCatalog = trimmedEmoteCatalog;
            EmoteProvider = emoteProvider;
            WearablesProvider = wearablesProvider;
            ThirdPartyNftProviderSource = thirdPartyNftProviderSource;
            BuilderContentURL = builderContentURL;
            EmotesEventBus = emotesEventBus;
            EmoteWheelShortcutHandler = emoteWheelShortcutHandler;
            BackpackEventBus = backpackEventBus;
            ThumbnailProvider = thumbnailProvider;
        }

        public static WearableContainer Create(
            StaticContainer staticContainer,
            BootstrapContainer bootstrapContainer,
            IWeb3IdentityCache identityCache,
            World globalWorld,
            IAppArgs appArgs,
            bool enableAnalytics)
        {
            IEventBus emotesEventBus = new EventBus(true);

            var coreBackpackEventBus = new BackpackEventBus();

            IBackpackEventBus backpackEventBus = enableAnalytics
                ? new BackpackEventBusAnalyticsDecorator(coreBackpackEventBus, bootstrapContainer.Analytics.Controller)
                : coreBackpackEventBus;

            var builderDTOsURL = URLDomain.FromString(bootstrapContainer.DecentralandUrlsSource.Url(DecentralandUrl.BuilderApiDtos));
            var builderContentURL = URLDomain.FromString(bootstrapContainer.DecentralandUrlsSource.Url(DecentralandUrl.BuilderApiContent));

            // If we have many undesired delays when using the third-party providers, it might be useful to cache it at app's bootstrap
            // So far, the chance of using it is quite low, so it's preferable to do it lazy avoiding extra requests & memory allocations
            IThirdPartyNftProviderSource thirdPartyNftProviderSource = new RealmThirdPartyNftProviderSource(staticContainer.WebRequestsContainer.WebRequestController,
                bootstrapContainer.DecentralandUrlsSource);

            var trimmedEmoteCatalog = new TrimmedEmoteStorage();
            staticContainer.CacheCleaner.Register(trimmedEmoteCatalog);

            IEmoteProvider emoteProvider = new ApplicationParamsEmoteProvider(appArgs,
                new EcsEmoteProvider(globalWorld, identityCache), builderDTOsURL.Value);

            var wearablesProvider = new ApplicationParametersWearablesProvider(appArgs,
                new ECSWearablesProvider(identityCache, globalWorld), builderDTOsURL.Value);

            return new WearableContainer(
                new WearableStorage(),
                new TrimmedWearableStorage(),
                trimmedEmoteCatalog,
                emoteProvider,
                wearablesProvider,
                thirdPartyNftProviderSource,
                builderContentURL,
                emotesEventBus,
                new EmoteWheelShortcutHandler(emotesEventBus),
                backpackEventBus,
                new ECSThumbnailProvider(bootstrapContainer.DecentralandUrlsSource, globalWorld));
        }

        public WearablePlugin CreateWearablePlugin(StaticContainer staticContainer, BootstrapContainer bootstrapContainer) =>
            new (
                staticContainer.WebRequestsContainer.WebRequestController,
                staticContainer.RealmData,
                bootstrapContainer.DecentralandUrlsSource,
                staticContainer.CacheCleaner,
                WearableCatalog,
                trimmedWearableCatalog,
                bootstrapContainer.Analytics.EntitiesAnalytics,
                BuilderContentURL.Value);

        public EmotePlugin CreateEmotePlugin(
            StaticContainer staticContainer,
            BootstrapContainer bootstrapContainer,
            IAssetsProvisioner assetsProvisioner,
            IDebugContainerBuilder debugBuilder,
            UIShellContainer uiShellContainer,
            ProfileContainer profileContainer,
            CommsContainer commsContainer,
            IEmotesMessageBus emotesMessageBus,
            World globalWorld,
            Entity playerEntity) =>
            new (
                staticContainer.WebRequestsContainer.WebRequestController,
                staticContainer.EmoteStorage,
                staticContainer.RealmData,
                emotesMessageBus,
                debugBuilder,
                assetsProvisioner,
                profileContainer.SelfProfile,
                uiShellContainer.MvcManager,
                staticContainer.CacheCleaner,
                commsContainer.EntityParticipantTable,
                uiShellContainer.Cursor,
                staticContainer.InputBlock,
                globalWorld,
                playerEntity,
                BuilderContentURL.Value,
                ThumbnailProvider,
                staticContainer.ScenesCache,
                bootstrapContainer.DecentralandUrlsSource,
                bootstrapContainer.Analytics.EntitiesAnalytics,
                EmotesEventBus,
                TrimmedEmoteCatalog,
                staticContainer.EmotesContainer.EmotePlayer);
    }
}
