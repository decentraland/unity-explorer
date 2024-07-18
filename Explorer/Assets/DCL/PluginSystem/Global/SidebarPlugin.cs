using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.ExplorePanel;
using DCL.Notification.NotificationsBus;
using DCL.Profiles;
using DCL.UI.MainUI;
using DCL.UI.Sidebar;
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

        public SidebarPlugin(IAssetsProvisioner assetsProvisioner, IMVCManager mvcManager, MainUIContainer mainUIContainer, INotificationsBusController notificationsBusController, IWeb3IdentityCache web3IdentityCache,
            IProfileRepository profileRepository, IWebRequestController webRequestController)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.mainUIContainer = mainUIContainer;
            this.notificationsBusController = notificationsBusController;
            this.web3IdentityCache = web3IdentityCache;
            this.profileRepository = profileRepository;
            this.webRequestController = webRequestController;
        }

        protected override void InjectSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        protected override async UniTask<ContinueInitialization?> InitializeInternalAsync(SidebarSettings settings, CancellationToken ct)
        {
            return (ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            {
                mvcManager.RegisterController(new SidebarController(() =>
                    {
                        SidebarView? view = mainUIContainer.SidebarView;
                        view.gameObject.SetActive(true);
                        return view;
                    },
                    mvcManager,
                    new ProfileWidgetController(() => mainUIContainer.SidebarView.ProfileWidget, web3IdentityCache, profileRepository, webRequestController)
                ));
            };
        }

        public class SidebarSettings : IDCLPluginSettings { }
    }
}
