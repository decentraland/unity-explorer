using Arch.SystemGroups;
using DCL.Minimap;
using MVC;

namespace DCL.PluginSystem.Global
{
    public class MinimapPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly MinimapController minimapController;

        public MinimapPlugin(IMVCManager mvcManager, MinimapController minimapController)
        {
            this.minimapController = minimapController;
            mvcManager.RegisterController(minimapController);
        }

        public void Dispose()
        {
            minimapController.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            TrackPlayerPositionSystem? trackPlayerPositionSystem = TrackPlayerPositionSystem.InjectToWorld(ref builder);
            minimapController.HookPlayerPositionTrackingSystem(trackPlayerPositionSystem);
        }
    }
}
