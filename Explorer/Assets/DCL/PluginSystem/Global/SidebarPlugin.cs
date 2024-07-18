using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.UI.MainUI;
using DCL.UI.Sidebar;
using MVC;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class SidebarPlugin : DCLGlobalPluginBase<SidebarPlugin.SidebarSettings>
    {

        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IMVCManager mvcManager;
        private readonly MainUIContainer mainUIContainer;

        public SidebarPlugin(IAssetsProvisioner assetsProvisioner, IMVCManager mvcManager, MainUIContainer mainUIContainer)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.mvcManager = mvcManager;
            this.mainUIContainer = mainUIContainer;
        }

        protected override void InjectSystems(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        protected override async UniTask<ContinueInitialization?> InitializeInternalAsync(SidebarSettings settings, CancellationToken ct)
        {
            return (ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) =>
            {
                mvcManager.RegisterController(new SidebarController(() =>
                {
                    var view = mainUIContainer.SidebarView;
                    view.gameObject.SetActive(true);
                    return view;
                }));
            };
        }


        public class SidebarSettings : IDCLPluginSettings
        {
        }
    }
}
