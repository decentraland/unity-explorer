using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.AvatarRendering.Emotes;
using DCL.AvatarRendering.Emotes.Equipped;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Equipped;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.ThirdParty;
using DCL.Backpack;
using DCL.Backpack.BackpackBus;
using DCL.Browser;
using DCL.CharacterPreview;
using DCL.Chat;
using DCL.ExplorePanel;
using DCL.Input;
using DCL.Landscape.Settings;
using DCL.MapRenderer;
using DCL.Navmap;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.PlacesAPIService;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.Quality;
using DCL.Settings;
using DCL.Settings.Configuration;
using DCL.UI.ProfileElements;
using DCL.UserInAppInitializationFlow;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using ECS.Prioritization;
using ECS.SceneLifeCycle.Realm;
using Global.Dynamic;
using MVC;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Audio;

// ReSharper disable UnusedAutoPropertyAccessor.Local
namespace DCL.PluginSystem.Global
{
    public class ExplorePanelPlugin : DCLGlobalPluginBase<ExplorePanelPlugin.ExplorePanelSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly MapRendererContainer mapRendererContainer;
        private readonly IMVCManager mvcManager;
        private readonly IPlacesAPIService placesAPIService;
        private readonly IProfileRepository profileRepository;
        private readonly IUserInAppInitializationFlow userInAppInitializationFlow;
        private readonly ISelfProfile selfProfile;
        private readonly IEquippedWearables equippedWearables;
        private readonly IEquippedEmotes equippedEmotes;
        private readonly IWeb3Authenticator web3Authenticator;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IWearableCache wearableCache;
        private readonly ICharacterPreviewFactory characterPreviewFactory;
        private readonly IWebBrowser webBrowser;
        private readonly IRealmNavigator realmNavigator;
        private readonly IEmoteCache emoteCache;
        private readonly DCLInput dclInput;
        private readonly IWebRequestController webRequestController;
        private readonly CharacterPreviewEventBus characterPreviewEventBus;
        private readonly IBackpackEventBus backpackEventBus;
        private readonly IThirdPartyNftProviderSource thirdPartyNftProviderSource;
        private readonly IWearablesProvider wearablesProvider;
        private readonly ICursor cursor;
        private readonly IEmoteProvider emoteProvider;
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;

        private readonly IMapPathEventBus mapPathEventBus;
        private readonly ICollection<string> forceRender;
        private ExplorePanelInputHandler? inputHandler;
        private readonly IRealmData realmData;
        private readonly IProfileCache profileCache;
        private readonly URLDomain assetBundleURL;
        private readonly INotificationsBusController notificationsBusController;
        private readonly ChatEntryConfigurationSO chatEntryConfiguration;

        private NavmapController? navmapController;
        private SettingsController? settingsController;
        private BackpackSubPlugin? backpackSubPlugin;

        public ExplorePanelPlugin(IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            MapRendererContainer mapRendererContainer,
            IPlacesAPIService placesAPIService,
            IWebRequestController webRequestController,
            IWeb3IdentityCache web3IdentityCache,
            IWearableCache wearableCache,
            ICharacterPreviewFactory characterPreviewFactory,
            IProfileRepository profileRepository,
            IWeb3Authenticator web3Authenticator,
            IUserInAppInitializationFlow userInAppInitializationFlow,
            ISelfProfile selfProfile,
            IEquippedWearables equippedWearables,
            IEquippedEmotes equippedEmotes,
            IWebBrowser webBrowser,
            IEmoteCache emoteCache,
            IRealmNavigator realmNavigator,
            ICollection<string> forceRender,
            DCLInput dclInput,
            IRealmData realmData,
            IProfileCache profileCache,
            URLDomain assetBundleURL,
            INotificationsBusController notificationsBusController,
            CharacterPreviewEventBus characterPreviewEventBus,
            IMapPathEventBus mapPathEventBus,
            ChatEntryConfigurationSO chatEntryConfiguration,
            IBackpackEventBus backpackEventBus,
            IThirdPartyNftProviderSource thirdPartyNftProviderSource,
            IWearablesProvider wearablesProvider,
            ICursor cursor,
            IEmoteProvider emoteProvider,
            Arch.Core.World world,
            Entity playerEntity)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.mapRendererContainer = mapRendererContainer;
            this.placesAPIService = placesAPIService;
            this.webRequestController = webRequestController;
            this.web3IdentityCache = web3IdentityCache;
            this.wearableCache = wearableCache;
            this.characterPreviewFactory = characterPreviewFactory;
            this.profileRepository = profileRepository;
            this.web3Authenticator = web3Authenticator;
            this.userInAppInitializationFlow = userInAppInitializationFlow;
            this.selfProfile = selfProfile;
            this.equippedWearables = equippedWearables;
            this.equippedEmotes = equippedEmotes;
            this.webBrowser = webBrowser;
            this.realmNavigator = realmNavigator;
            this.forceRender = forceRender;
            this.realmData = realmData;
            this.profileCache = profileCache;
            this.assetBundleURL = assetBundleURL;
            this.notificationsBusController = notificationsBusController;
            this.emoteCache = emoteCache;
            this.dclInput = dclInput;
            this.characterPreviewEventBus = characterPreviewEventBus;
            this.mapPathEventBus = mapPathEventBus;
            this.chatEntryConfiguration = chatEntryConfiguration;
            this.backpackEventBus = backpackEventBus;
            this.thirdPartyNftProviderSource = thirdPartyNftProviderSource;
            this.wearablesProvider = wearablesProvider;
            this.cursor = cursor;
            this.emoteProvider = emoteProvider;
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public override void Dispose()
        {
            navmapController?.Dispose();
            settingsController?.Dispose();
            backpackSubPlugin?.Dispose();
            inputHandler?.Dispose();
        }

        protected override async UniTask<ContinueInitialization?> InitializeInternalAsync(ExplorePanelSettings settings, CancellationToken ct)
        {
            backpackSubPlugin = new BackpackSubPlugin(
                assetsProvisioner,
                web3IdentityCache,
                characterPreviewFactory,
                wearableCache,
                selfProfile,
                equippedWearables,
                equippedEmotes,
                emoteCache,
                settings.EmbeddedEmotesAsURN(),
                forceRender,
                realmData,
                dclInput,
                assetBundleURL,
                webRequestController,
                characterPreviewEventBus,
                backpackEventBus,
                thirdPartyNftProviderSource,
                wearablesProvider,
                cursor,
                emoteProvider,
                world,
                playerEntity
            );

            ExplorePanelView panelViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.ExplorePanelPrefab, ct: ct)).GetComponent<ExplorePanelView>();
            ControllerBase<ExplorePanelView, ExplorePanelParameter>.ViewFactoryMethod viewFactoryMethod = ExplorePanelController.Preallocate(panelViewAsset, null, out ExplorePanelView explorePanelView);

            ProvidedAsset<SettingsMenuConfiguration> settingsMenuConfiguration = await assetsProvisioner.ProvideMainAssetAsync(settings.SettingsMenuConfiguration, ct);
            ProvidedAsset<AudioMixer> generalAudioMixer = await assetsProvisioner.ProvideMainAssetAsync(settings.GeneralAudioMixer, ct);
            ProvidedAsset<RealmPartitionSettingsAsset> realmPartitionSettings = await assetsProvisioner.ProvideMainAssetAsync(settings.RealmPartitionSettings, ct);

            ProvidedAsset<LandscapeData> landscapeData = await assetsProvisioner.ProvideMainAssetAsync(settings.LandscapeData, ct);
            ProvidedAsset<QualitySettingsAsset> qualitySettingsAsset = await assetsProvisioner.ProvideMainAssetAsync(settings.QualitySettingsAsset, ct);
            settingsController = new SettingsController(explorePanelView.GetComponentInChildren<SettingsView>(), settingsMenuConfiguration.Value, generalAudioMixer.Value, realmPartitionSettings.Value, landscapeData.Value, qualitySettingsAsset.Value);

            navmapController = new NavmapController(navmapView: explorePanelView.GetComponentInChildren<NavmapView>(), mapRendererContainer.MapRenderer, placesAPIService, webRequestController, webBrowser, dclInput, realmNavigator, realmData, mapPathEventBus, world, playerEntity);

            await navmapController.InitializeAssetsAsync(assetsProvisioner, ct);
            ContinueInitialization backpackInitialization = await backpackSubPlugin.InitializeAsync(settings.BackpackSettings, explorePanelView.GetComponentInChildren<BackpackView>(), ct);

            mvcManager.RegisterController(new
                ExplorePanelController(viewFactoryMethod, navmapController, settingsController, backpackSubPlugin.backpackController!, playerEntity, world,
                    new ProfileWidgetController(() => explorePanelView.ProfileWidget, web3IdentityCache, profileRepository, webRequestController),
                    new ProfileMenuController(() => explorePanelView.ProfileMenuView, explorePanelView.ProfileMenuView.ProfileMenu, web3IdentityCache, profileRepository, webRequestController, world, playerEntity, webBrowser, web3Authenticator, userInAppInitializationFlow, profileCache, mvcManager, chatEntryConfiguration),
                    dclInput, notificationsBusController, mvcManager));

            inputHandler = new ExplorePanelInputHandler(dclInput, mvcManager);

            return (ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
                backpackInitialization.Invoke(ref builder, arguments);
        }

        protected override void InjectSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public class ExplorePanelSettings : IDCLPluginSettings
        {
            [field: Header(nameof(ExplorePanelPlugin) + "." + nameof(ExplorePanelSettings))]
            [field: Space]
            [field: SerializeField]
            public AssetReferenceGameObject ExplorePanelPrefab;

            [field: SerializeField]
            public BackpackSettings BackpackSettings { get; private set; }

            [field: SerializeField]
            public string[] EmbeddedEmotes { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<SettingsMenuConfiguration> SettingsMenuConfiguration { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<AudioMixer> GeneralAudioMixer { get; private set; }

            [field: SerializeField]
            public StaticSettings.RealmPartitionSettingsRef RealmPartitionSettings { get; private set; }

            [field: SerializeField]
            public LandscapeSettings.LandscapeDataRef LandscapeData { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<QualitySettingsAsset> QualitySettingsAsset { get; private set; }

            public IReadOnlyCollection<URN> EmbeddedEmotesAsURN() =>
                EmbeddedEmotes.Select(s => new URN(s)).ToArray();
        }
    }
}
