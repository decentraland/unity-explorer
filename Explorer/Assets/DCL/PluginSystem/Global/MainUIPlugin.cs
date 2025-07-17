using Arch.SystemGroups;
using DCL.UI.MainUI;
using DCL.UI.SharedSpaceManager;
using MVC;

namespace DCL.PluginSystem.Global
{
    public class MainUIPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IMVCManager mvcManager;

        public MainUIPlugin(
            IMVCManager mvcManager,
            MainUIView mainUIView,
            ISharedSpaceManager sharedSpaceManager)
        {
            this.mvcManager = mvcManager;

            var mainUIController = new MainUIController(
                () =>
                {
                    mainUIView.gameObject.SetActive(true);
                    return mainUIView;
                },
                mvcManager,
                sharedSpaceManager
            );

            mvcManager.RegisterController(mainUIController);
        }

        public void Dispose()
        {
            mvcManager.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments) { }
    }
}
