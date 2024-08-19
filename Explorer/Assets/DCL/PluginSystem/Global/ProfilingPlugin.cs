using Arch.SystemGroups;
using DCL.DebugUtilities;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using DCL.Profiling.ECS;
using ECS;

namespace DCL.PluginSystem.Global
{
    public class ProfilingPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IDebugViewProfiler profiler;
        private readonly IRealmData realmData;
        private readonly MemoryBudget memoryBudget;
        private readonly IDebugContainerBuilder debugContainerBuilder;

        public ProfilingPlugin(IDebugViewProfiler profiler, IRealmData realmData, MemoryBudget memoryBudget, IDebugContainerBuilder debugContainerBuilder)
        {
            this.profiler = profiler;
            this.realmData = realmData;
            this.debugContainerBuilder = debugContainerBuilder;
            this.memoryBudget = memoryBudget;
        }

        public void Dispose()
        {
            profiler.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            DebugViewProfilingSystem.InjectToWorld(ref builder, realmData, profiler, memoryBudget, debugContainerBuilder, arguments.V8EngineFactory);
        }
    }
}
