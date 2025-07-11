using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Backpack;
using DCL.Browser;
using DCL.Chat.History;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Notifications;
using DCL.Notifications.NotificationsMenu;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.Profiles;
using DCL.Profiles.Self;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SkyBox;
using DCL.UI.Controls;
using DCL.UI.MainUI;
using DCL.UI.ProfileElements;
using DCL.UI.Profiles;
using DCL.UI.Profiles.Helpers;
using DCL.UI.SharedSpaceManager;
using DCL.UI.Sidebar;
using DCL.UI.Skybox;
using DCL.UserInAppInitializationFlow;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests;
using ECS;
using MVC;
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
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;
        private readonly bool includeCameraReel;
        private readonly bool includeFriends;
        private readonly bool includeMarketplaceCredits;
        private readonly IChatHistory chatHistory;
        private readonly ProfileRepositoryWrapper profileRepositoryWrapper;
        private readonly ISharedSpaceManager sharedSpaceManager;
        private readonly ProfileChangesBus profileChangesBus;
        private readonly ISelfProfile selfProfile;
        private readonly IRealmData realmData;
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;
        private readonly IDecentralandUrlsSource decentralandUrls;

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
            Arch.Core.World world,
            Entity playerEntity,
            bool includeCameraReel,
            bool includeFriends,
            bool includeMarketplaceCredits,
            IChatHistory chatHistory,
            ProfileRepositoryWrapper profileDataProvider,
            ISharedSpaceManager sharedSpaceManager,
            ProfileChangesBus profileChangesBus,
            ISelfProfile selfProfile,
            IRealmData realmData,
            ISceneRestrictionBusController sceneRestrictionBusController,
            IDecentralandUrlsSource decentralandUrls)
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
            this.world = world;
            this.playerEntity = playerEntity;
            this.includeCameraReel = includeCameraReel;
            this.includeFriends = includeFriends;
            this.includeMarketplaceCredits = includeMarketplaceCredits;
            this.chatHistory = chatHistory;
            this.profileRepositoryWrapper = profileDataProvider;
            this.sharedSpaceManager = sharedSpaceManager;
            this.profileChangesBus = profileChangesBus;
            this.selfProfile = selfProfile;
            this.realmData = realmData;
            this.sceneRestrictionBusController = sceneRestrictionBusController;
            this.decentralandUrls = decentralandUrls;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(SidebarSettings settings, CancellationToken ct)
        {
            NotificationIconTypes notificationIconTypes = (await assetsProvisioner.ProvideMainAssetAsync(settings.NotificationIconTypesSO, ct)).Value;
            NftTypeIconSO rarityBackgroundMapping = await assetsProvisioner.ProvideMainAssetValueAsync(settings.RarityColorMappings, ct);

            ControlsPanelView panelViewAsset = (await assetsProvisioner.ProvideMainAssetValueAsync(settings.ControlsPanelPrefab, ct)).GetComponent<ControlsPanelView>();
            ControlsPanelController.Preallocate(panelViewAsset, null!, out ControlsPanelView controlsPanelView);

            mvcManager.RegisterController(new SidebarController(() =>
                {
                    SidebarView view = mainUIView.SidebarView;
                    view.gameObject.SetActive(true);
                    return view;
                },
                mvcManager,
                notificationsBusController,
                new NotificationsMenuController(mainUIView.SidebarView.NotificationsMenuView, notificationsRequestController, notificationsBusController, notificationIconTypes, webRequestController, rarityBackgroundMapping, web3IdentityCache, profileRepositoryWrapper),
                new ProfileWidgetController(() => mainUIView.SidebarView.ProfileWidget, web3IdentityCache, profileRepository, profileChangesBus, profileRepositoryWrapper),
                new ProfileMenuController(() => mainUIView.SidebarView.ProfileMenuView, web3IdentityCache, profileRepository, world, playerEntity, webBrowser, web3Authenticator, userInAppInitializationFlow, profileCache, mvcManager, profileRepositoryWrapper),
                new SkyboxMenuController(() => mainUIView.SidebarView.SkyboxMenuView, settings.SettingsAsset),
                new ControlsPanelController(() => controlsPanelView, mvcManager),
                webBrowser,
                includeCameraReel,
                includeFriends,
                includeMarketplaceCredits,
                mainUIView.ChatView,
                chatHistory,
                sharedSpaceManager,
                selfProfile,
                realmData,
                sceneRestrictionBusController,
                decentralandUrls
            ));
        }

        public class SidebarSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public AssetReferenceT<NotificationIconTypes> NotificationIconTypesSO { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<NftTypeIconSO> RarityColorMappings { get; private set; }

            [field: SerializeField]
            public SkyboxSettingsAsset SettingsAsset { get; private set; }

            [field: SerializeField]
            public AssetReferenceGameObject ControlsPanelPrefab;
        }
    }
}
