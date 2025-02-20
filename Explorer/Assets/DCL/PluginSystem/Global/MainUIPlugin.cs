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
        private readonly bool isFriendsEnabled;

        public MainUIPlugin(
            IMVCManager mvcManager,
            ISidebarBus sidebarBus,
            MainUIView mainUIView,
            bool isFriendsEnabled)
        {
            this.mvcManager = mvcManager;
            this.sidebarBus = sidebarBus;
            this.mainUIView = mainUIView;
            this.isFriendsEnabled = isFriendsEnabled;
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
                mvcManager,
                isFriendsEnabled
            );

            mvcManager.RegisterController(mainUIController);
        }

        public class Settings : IDCLPluginSettings { }
    }
}
