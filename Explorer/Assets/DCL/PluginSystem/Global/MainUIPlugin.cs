using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.UI.MainUI;
using DCL.UI.SharedSpaceManager;
using MVC;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class MainUIPlugin : IDCLGlobalPlugin<NoExposedPluginSettings>
    {
        private readonly IMVCManager mvcManager;
        private readonly MainUIView mainUIView;
        private readonly bool isFriendsEnabled;
        private readonly ISharedSpaceManager sharedSpaceManager;

        public MainUIPlugin(
            IMVCManager mvcManager,
            MainUIView mainUIView,
            bool isFriendsEnabled,
            ISharedSpaceManager sharedSpaceManager)
        {
            this.mvcManager = mvcManager;
            this.mainUIView = mainUIView;
            this.isFriendsEnabled = isFriendsEnabled;
            this.sharedSpaceManager = sharedSpaceManager;
        }

        public void Dispose()
        {
            mvcManager.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }

        public UniTask InitializeAsync(NoExposedPluginSettings _, CancellationToken ct)
        {
            var mainUIController = new MainUIController(
                () =>
                {
                    mainUIView.gameObject.SetActive(true);
                    return mainUIView;
                },
                mvcManager,
                isFriendsEnabled,
                sharedSpaceManager
            );

            mvcManager.RegisterController(mainUIController);
            return UniTask.CompletedTask;
        }
    }
}
