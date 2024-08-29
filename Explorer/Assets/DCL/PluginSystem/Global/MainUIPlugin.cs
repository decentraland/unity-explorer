using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.SidebarBus;
using DCL.UI.MainUI;
using MVC;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class MainUIPlugin : IDCLGlobalPlugin<MainUIPlugin.Settings>
    {
        private readonly IMVCManager mvcManager;
        private readonly ISidebarBus sidebarBus;
        private readonly MainUIView mainUIView;

        public MainUIPlugin(
            IMVCManager mvcManager,
            ISidebarBus sidebarBus,
            MainUIView mainUIView)
        {
            this.mvcManager = mvcManager;
            this.sidebarBus = sidebarBus;
            this.mainUIView = mainUIView;
        }

        public void Dispose()
        {
            mvcManager.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public async UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            var mainUIController = new MainUIController(
                () =>
                {
                    MainUIView view = mainUIView;
                    view.gameObject.SetActive(true);
                    return view;
                },
                sidebarBus,
                mvcManager
            );

            mvcManager.RegisterController(mainUIController);
        }

        public class Settings : IDCLPluginSettings { }
    }
}
