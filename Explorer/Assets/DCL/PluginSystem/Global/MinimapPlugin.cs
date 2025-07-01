using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Minimap;
using MVC;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class MinimapPlugin : IDCLGlobalPlugin<NoExposedPluginSettings>
    {
        private readonly IMVCManager mvcManager;
        private readonly MinimapController minimapController;

        public MinimapPlugin(IMVCManager mvcManager, MinimapController minimapController)
        {
            this.mvcManager = mvcManager;
            this.minimapController = minimapController;
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

        public UniTask InitializeAsync(NoExposedPluginSettings _, CancellationToken ct)
        {
            mvcManager.RegisterController(minimapController);
            return UniTask.CompletedTask;
        }
    }
}
