using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using MVC;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class CommunitiesPlugin : IDCLGlobalPlugin<CommunitiesPluginSettings>
    {
        private readonly IMVCManager mvcManager;

        public CommunitiesPlugin(IMVCManager mvcManager)
        {
            this.mvcManager = mvcManager;
        }

        public void Dispose()
        {
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
        }

        public UniTask InitializeAsync(CommunitiesPluginSettings settings, CancellationToken ct)
        {
            return UniTask.CompletedTask;
        }
    }

    public class CommunitiesPluginSettings : IDCLPluginSettings { }
}
