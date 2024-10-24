using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Backpack;
using DCL.Browser;
using DCL.Chat;
using DCL.Notifications;
using DCL.Notifications.NotificationsMenu;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.Profiles;
using DCL.SidebarBus;
using DCL.UI.MainUI;
using DCL.UI.ProfileElements;
using DCL.UI.Sidebar;
using DCL.UserInAppInitializationFlow;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests;
using DCL.WebRequests.ArgsFactory;
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
        private readonly IGetTextureArgsFactory getTextureArgsFactory;
        private readonly IWebBrowser webBrowser;
        private readonly IWeb3Authenticator web3Authenticator;
        private readonly IUserInAppInitializationFlow userInAppInitializationFlow;
        private readonly IProfileCache profileCache;
        private readonly ISidebarBus sidebarBus;
        private readonly ChatEntryConfigurationSO chatEntryConfigurationSo;
        private readonly Arch.Core.World world;
        private readonly Entity playerEntity;

        public SidebarPlugin(
            IAssetsProvisioner assetsProvisioner,
            IMVCManager mvcManager,
            MainUIView mainUIView,
            INotificationsBusController notificationsBusController,
            NotificationsRequestController notificationsRequestController,
            IWeb3IdentityCache web3IdentityCache,
            IProfileRepository profileRepository,
            IWebRequestController webRequestController,
            IGetTextureArgsFactory getTextureArgsFactory,
            IWebBrowser webBrowser,
            IWeb3Authenticator web3Authenticator,
            IUserInAppInitializationFlow userInAppInitializationFlow,
            IProfileCache profileCache,
            ISidebarBus sidebarBus,
            ChatEntryConfigurationSO chatEntryConfigurationSo,
            Arch.Core.World world,
            Entity playerEntity)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.mainUIView = mainUIView;
            this.notificationsBusController = notificationsBusController;
            this.notificationsRequestController = notificationsRequestController;
            this.web3IdentityCache = web3IdentityCache;
            this.profileRepository = profileRepository;
            this.webRequestController = webRequestController;
            this.getTextureArgsFactory = getTextureArgsFactory;
            this.webBrowser = webBrowser;
            this.web3Authenticator = web3Authenticator;
            this.userInAppInitializationFlow = userInAppInitializationFlow;
            this.profileCache = profileCache;
            this.sidebarBus = sidebarBus;
            this.chatEntryConfigurationSo = chatEntryConfigurationSo;
            this.world = world;
            this.playerEntity = playerEntity;
        }

        public void Dispose() { }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(SidebarSettings settings, CancellationToken ct)
        {
            NotificationIconTypes notificationIconTypes = (await assetsProvisioner.ProvideMainAssetAsync(settings.NotificationIconTypesSO, ct: ct)).Value;
            NftTypeIconSO rarityBackgroundMapping = await assetsProvisioner.ProvideMainAssetValueAsync(settings.RarityColorMappings, ct);

            mvcManager.RegisterController(new SidebarController(() =>
                {
                    SidebarView view = mainUIView.SidebarView;
                    view.gameObject.SetActive(true);
                    return view;
                },
                mvcManager,
                notificationsBusController,
                new NotificationsMenuController(mainUIView.SidebarView.NotificationsMenuView, notificationsRequestController, notificationsBusController, notificationIconTypes, webRequestController, getTextureArgsFactory, sidebarBus, rarityBackgroundMapping, web3IdentityCache),
                new ProfileWidgetController(() => mainUIView.SidebarView.ProfileWidget, web3IdentityCache, profileRepository, webRequestController, getTextureArgsFactory),
                new ProfileMenuController(() => mainUIView.SidebarView.ProfileMenuView, web3IdentityCache, profileRepository, webRequestController, getTextureArgsFactory, world, playerEntity, webBrowser, web3Authenticator, userInAppInitializationFlow, profileCache, mvcManager, chatEntryConfigurationSo),
                sidebarBus,
                chatEntryConfigurationSo,
                web3IdentityCache,
                profileRepository,
                webBrowser
            ));
        }

        public class SidebarSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public AssetReferenceT<NotificationIconTypes> NotificationIconTypesSO { get; private set; }

            [field: SerializeField]
            public AssetReferenceT<NftTypeIconSO> RarityColorMappings { get; private set; }
        }
    }
}
