using Arch.Core;
using Arch.SystemGroups;
using DCL.DebugUtilities;
using DCL.PluginSystem.Global;

namespace DCL.Allocators
{
    public class AllocatorsPlugin : IDCLGlobalPlugin
    {
        private readonly IDebugContainerBuilder debugBuilder;

        public AllocatorsPlugin(IDebugContainerBuilder debugBuilder)
        {
            this.debugBuilder = debugBuilder;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
          DebugAllocatorsSystem.InjectToWorld(ref builder, debugBuilder);
        }

        public void Dispose()
        {
            // ignore
        }
    }
}
