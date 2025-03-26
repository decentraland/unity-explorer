using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Backpack;
using DCL.Browser;
using DCL.Chat.History;
using DCL.Input;
using DCL.MarketplaceCredits;
using DCL.MarketplaceCreditsAPIService;
using DCL.Notifications;
using DCL.Notifications.NotificationsMenu;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.SidebarBus;
using DCL.StylizedSkybox.Scripts;
using DCL.UI.Controls;
using DCL.UI.MainUI;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles;
using DCL.UI.Sidebar;
using DCL.UI.Sidebar.SidebarActionsBus;
using DCL.UI.Skybox;
using DCL.UserInAppInitializationFlow;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests;
using MVC;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class SidebarPlugin : IDCLGlobalPlugin<SidebarPlugin.SidebarSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly MainUIView mainUIView;
        private readonly INotificationsBusController notificationsBusController;
        private readonly NotificationsRequestController notificationsRequestController;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IProfileRepository profileRepository;
        private readonly IWebRequestController webRequestController;
        private readonly IWebBrowser webBrowser;
        private readonly IWeb3Authenticator web3Authenticator;
        private readonly IUserInAppInitializationFlow userInAppInitializationFlow;
        private readonly IProfileCache profileCache;
        private readonly ISidebarBus sidebarBus;
        private readonly DCLInput input;
        private readonly ISidebarActionsBus sidebarActionsBus;
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;
        private readonly bool includeCameraReel;
        private readonly bool includeFriends;
        private readonly bool includeMarketplaceCredits;
        private readonly IChatHistory chatHistory;
        private readonly ViewDependencies viewDependencies;
        private readonly IInputBlock inputBlock;
        private readonly MarketplaceCreditsAPIClient marketplaceCreditsAPIClient;
        private readonly ISelfProfile selfProfile;

        public SidebarPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            MainUIView mainUIView,
            INotificationsBusController notificationsBusController,
            NotificationsRequestController notificationsRequestController,
            IWeb3IdentityCache web3IdentityCache,
            IProfileRepository profileRepository,
            IWebRequestController webRequestController,
            IWebBrowser webBrowser,
            IWeb3Authenticator web3Authenticator,
            IUserInAppInitializationFlow userInAppInitializationFlow,
            IProfileCache profileCache,
            ISidebarBus sidebarBus,
            DCLInput input,
            ISidebarActionsBus sidebarActionsBus,
            Arch.Core.World world,
            Entity playerEntity,
            bool includeCameraReel,
            bool includeFriends,
            bool includeMarketplaceCredits,
            IChatHistory chatHistory,
            ViewDependencies viewDependencies,
            IInputBlock inputBlock,
            MarketplaceCreditsAPIClient marketplaceCreditsAPIClient,
            ISelfProfile selfProfile)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.mainUIView = mainUIView;
            this.notificationsBusController = notificationsBusController;
            this.notificationsRequestController = notificationsRequestController;
            this.web3IdentityCache = web3IdentityCache;
            this.profileRepository = profileRepository;
            this.webRequestController = webRequestController;
            this.webBrowser = webBrowser;
            this.web3Authenticator = web3Authenticator;
            this.userInAppInitializationFlow = userInAppInitializationFlow;
            this.profileCache = profileCache;
            this.sidebarBus = sidebarBus;
            this.input = input;
            this.sidebarActionsBus = sidebarActionsBus;
            this.world = world;
            this.playerEntity = playerEntity;
            this.includeCameraReel = includeCameraReel;
            this.includeFriends = includeFriends;
            this.includeMarketplaceCredits = includeMarketplaceCredits;
            this.chatHistory = chatHistory;
            this.viewDependencies = viewDependencies;
            this.inputBlock = inputBlock;
            this.marketplaceCreditsAPIClient = marketplaceCreditsAPIClient;
            this.selfProfile = selfProfile;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(SidebarSettings settings, CancellationToken ct)
        {
            NotificationIconTypes notificationIconTypes = (await assetsProvisioner.ProvideMainAssetAsync(settings.NotificationIconTypesSO, ct)).Value;
            NftTypeIconSO rarityBackgroundMapping = await assetsProvisioner.ProvideMainAssetValueAsync(settings.RarityColorMappings, ct);

            ControlsPanelView panelViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.ControlsPanelPrefab, ct)).GetComponent<ControlsPanelView>();
            ControlsPanelController.Preallocate(panelViewAsset, null!, out ControlsPanelView controlsPanelView);

            CreditsUnlockedView creditsUnlockedView = (await assetsProvisioner.ProvideMainAssetAsync(settings.CreditsUnlockedPrefab, ct)).Value.GetComponent<CreditsUnlockedView>();
            CreditsUnlockedController creditsUnlockedController = new CreditsUnlockedController(CreditsUnlockedController.CreateLazily(creditsUnlockedView, null));

            mvcManager.RegisterController(creditsUnlockedController);
            mvcManager.RegisterController(new SidebarController(() =>
                {
                    SidebarView view = mainUIView.SidebarView;
                    view.gameObject.SetActive(true);
                    return view;
                },
                mvcManager,
                notificationsBusController,
                new NotificationsMenuController(mainUIView.SidebarView.NotificationsMenuView, notificationsRequestController, notificationsBusController, notificationIconTypes, webRequestController, mainUIView.SidebarView.notificationsButton, sidebarBus, rarityBackgroundMapping, web3IdentityCache),
                new MarketplaceCreditsMenuController(mainUIView.SidebarView.MarketplaceCreditsMenuView, mainUIView.SidebarView.MarketplaceCreditsButton, sidebarBus, sidebarActionsBus, webBrowser, inputBlock, marketplaceCreditsAPIClient, selfProfile, webRequestController, mvcManager, notificationsBusController, mainUIView.SidebarView.MarketplaceCreditsButtonAnimator, mainUIView.SidebarView.MarketplaceCreditsClaimIndicator),
                marketplaceCreditsAPIClient,
                new ProfileWidgetController(() => mainUIView.SidebarView.ProfileWidget, web3IdentityCache, profileRepository, viewDependencies),
                new ProfileMenuController(() => mainUIView.SidebarView.ProfileMenuView, web3IdentityCache, profileRepository, world, playerEntity, webBrowser, web3Authenticator, userInAppInitializationFlow, profileCache, mvcManager, viewDependencies),
                new SkyboxMenuController(() => mainUIView.SidebarView.SkyboxMenuView, settings.SkyboxSettingsAsset),
                new ControlsPanelController(() => controlsPanelView, mvcManager, input),
                sidebarBus,
                webBrowser,
                sidebarActionsBus,
                includeCameraReel,
                includeFriends,
                includeMarketplaceCredits,
                mainUIView.ChatView,
                chatHistory
            ));
        }

        public class SidebarSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public AssetReferenceT<NotificationIconTypes> NotificationIconTypesSO { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<NftTypeIconSO> RarityColorMappings { get; private set; }

            [field: SerializeField]
            public StylizedSkyboxSettingsAsset SkyboxSettingsAsset { get; private set; }

            [field: SerializeField]
            public AssetReferenceGameObject ControlsPanelPrefab;

            [field: SerializeField]
            public AssetReferenceGameObject CreditsUnlockedPrefab;
        }
    }
}
