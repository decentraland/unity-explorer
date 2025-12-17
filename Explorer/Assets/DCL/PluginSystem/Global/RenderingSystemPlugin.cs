using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.Rendering.RenderSystem;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class RenderingSystemPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IDebugContainerBuilder debugContainerBuilder;

        public RenderingSystemPlugin(IDebugContainerBuilder debugContainerBuilder)
        {
            this.debugContainerBuilder = debugContainerBuilder;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            DebugRenderSystem.InjectToWorld(ref builder, debugContainerBuilder);
        }

        public UniTask Initialize(IPluginSettingsContainer container, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void Dispose() { }
    }
}
