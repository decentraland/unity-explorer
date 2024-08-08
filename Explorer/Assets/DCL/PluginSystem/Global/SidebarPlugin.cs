using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Backpack;
using DCL.Browser;
using DCL.Chat;
using DCL.ExplorePanel;
using DCL.Notifications;
using DCL.Notifications.NotificationsMenu;
using DCL.NotificationsBusController.NotificationsBus;
using DCL.Profiles;
using DCL.SidebarBus;
using DCL.UI.MainUI;
using DCL.UI.Sidebar;
using DCL.UserInAppInitializationFlow;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests;
using MVC;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class SidebarPlugin : DCLGlobalPluginBase<SidebarPlugin.SidebarSettings>
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
        private readonly ChatEntryConfigurationSO chatEntryConfigurationSo;

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
            ChatEntryConfigurationSO chatEntryConfigurationSo)
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
            this.chatEntryConfigurationSo = chatEntryConfigurationSo;
        }

        protected override void InjectSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        protected override async UniTask<ContinueInitialization?> InitializeInternalAsync(SidebarSettings settings, CancellationToken ct)
        {
            NotificationIconTypes notificationIconTypes = (await assetsProvisioner.ProvideMainAssetAsync(settings.NotificationIconTypesSO, ct: ct)).Value;
            NftTypeIconSO rarityBackgroundMapping = await assetsProvisioner.ProvideMainAssetValueAsync(settings.RarityColorMappings, ct);
            return (ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            {
                mvcManager.RegisterController(new SidebarController(() =>
                    {
                        SidebarView view = mainUIView.SidebarView;
                        view.gameObject.SetActive(true);
                        return view;
                    },
                    mvcManager,
                    notificationsBusController,
                    new NotificationsMenuController(mainUIView.SidebarView.NotificationsMenuView, notificationsRequestController, notificationsBusController, notificationIconTypes, webRequestController, sidebarBus, rarityBackgroundMapping),
                    new ProfileWidgetController(() => mainUIView.SidebarView.ProfileWidget, web3IdentityCache, profileRepository, webRequestController),
                    new SidebarProfileController(() => mainUIView.SidebarView.ProfileMenuView, mainUIView.SidebarView.ProfileMenuView.ProfileMenu, web3IdentityCache, profileRepository, webRequestController, builder.World, arguments.PlayerEntity, webBrowser, web3Authenticator, userInAppInitializationFlow, profileCache, mvcManager, chatEntryConfigurationSo),
                    sidebarBus
                ));
            };
        }

        public class SidebarSettings : IDCLPluginSettings
        {
            [field: SerializeField]
            public AssetReferenceT<NotificationIconTypes> NotificationIconTypesSO { get; private set;}

            [field: SerializeField]
            public AssetReferenceT<NftTypeIconSO> RarityColorMappings { get; private set; }
        }
    }
}
