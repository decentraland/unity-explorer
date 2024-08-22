using Arch.SystemGroups;
using DCL.DebugUtilities;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using DCL.Profiling.ECS;
using ECS;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.CurrentScene;

namespace DCL.PluginSystem.Global
{
    public class ProfilingPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IDebugViewProfiler profiler;
        private readonly IRealmData realmData;
        private readonly MemoryBudget memoryBudget;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly IScenesCache scenesCache;

        public ProfilingPlugin(IDebugViewProfiler profiler, IRealmData realmData, MemoryBudget memoryBudget, IDebugContainerBuilder debugContainerBuilder, IScenesCache scenesCache)
        {
            this.profiler = profiler;
            this.realmData = realmData;
            this.debugContainerBuilder = debugContainerBuilder;
            this.scenesCache = scenesCache;
            this.memoryBudget = memoryBudget;
        }

        public void Dispose()
        {
            profiler.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            DebugViewProfilingSystem.InjectToWorld(ref builder, realmData, profiler, memoryBudget, debugContainerBuilder, arguments.V8ActiveEngines, scenesCache);
        }
    }
}
