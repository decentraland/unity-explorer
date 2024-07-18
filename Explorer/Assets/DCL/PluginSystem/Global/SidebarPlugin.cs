using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Browser;
using DCL.ExplorePanel;
using DCL.Notification.NotificationsBus;
using DCL.Profiles;
using DCL.UI.MainUI;
using DCL.UI.Sidebar;
using DCL.UserInAppInitializationFlow;
using DCL.Web3.Authenticators;
using DCL.Web3.Identities;
using DCL.WebRequests;
using MVC;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class SidebarPlugin : DCLGlobalPluginBase<SidebarPlugin.SidebarSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly MainUIContainer mainUIContainer;
        private readonly INotificationsBusController notificationsBusController;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly IProfileRepository profileRepository;
        private readonly IWebRequestController webRequestController;
        private readonly IWebBrowser webBrowser;
        private readonly IWeb3Authenticator web3Authenticator;
        private readonly IUserInAppInitializationFlow userInAppInitializationFlow;
        private readonly IProfileCache profileCache;

        public SidebarPlugin(IAssetsProvisioner assetsProvisioner, IMVCManager mvcManager, MainUIContainer mainUIContainer, INotificationsBusController notificationsBusController, IWeb3IdentityCache web3IdentityCache,
            IProfileRepository profileRepository, IWebRequestController webRequestController, IWebBrowser webBrowser, IWeb3Authenticator web3Authenticator, IUserInAppInitializationFlow userInAppInitializationFlow,
            IProfileCache profileCache)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.mainUIContainer = mainUIContainer;
            this.notificationsBusController = notificationsBusController;
            this.web3IdentityCache = web3IdentityCache;
            this.profileRepository = profileRepository;
            this.webRequestController = webRequestController;
            this.webBrowser = webBrowser;
            this.web3Authenticator = web3Authenticator;
            this.userInAppInitializationFlow = userInAppInitializationFlow;
            this.profileCache = profileCache;
        }

        protected override void InjectSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        protected override async UniTask<ContinueInitialization?> InitializeInternalAsync(SidebarSettings settings, CancellationToken ct)
        {
            return (ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            {
                mvcManager.RegisterController(new SidebarController(() =>
                    {
                        SidebarView view = mainUIContainer.SidebarView;
                        view.gameObject.SetActive(true);
                        return view;
                    },
                    mvcManager,
                    new ProfileWidgetController(() => mainUIContainer.SidebarView.ProfileWidget, web3IdentityCache, profileRepository, webRequestController),
                    new ProfileWidgetController(() => mainUIContainer.SidebarView.ProfileMenuWidget, web3IdentityCache, profileRepository, webRequestController),
                    new SystemMenuController(() => mainUIContainer.SidebarView.SystemMenuView, builder.World, arguments.PlayerEntity, webBrowser, web3Authenticator, userInAppInitializationFlow, profileCache, web3IdentityCache, mvcManager)
                ));
            };
        }

        public class SidebarSettings : IDCLPluginSettings { }
    }
}
