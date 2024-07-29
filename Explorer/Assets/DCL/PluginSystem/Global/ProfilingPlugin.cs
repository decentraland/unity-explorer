using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using System;
using System.Threading;
using ProfilingSystem = DCL.Profiling.ECS.ProfilingSystem;

namespace DCL.PluginSystem.Global
{
    public class ProfilingPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IProfilingProvider profilingProvider;
        private readonly FrameTimeCapBudget frameTimeCapBudget;
        private readonly MemoryBudget memoryBudget;
        private readonly IDebugContainerBuilder debugContainerBuilder;

        public ProfilingPlugin(IProfilingProvider profilingProvider, FrameTimeCapBudget frameTimeCapBudget, MemoryBudget memoryBudget, IDebugContainerBuilder debugContainerBuilder)
        {
            this.profilingProvider = profilingProvider;
            this.frameTimeCapBudget = frameTimeCapBudget;
            this.debugContainerBuilder = debugContainerBuilder;
            this.memoryBudget = memoryBudget;
        }

        public void Dispose()
        {
            profilingProvider.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ProfilingSystem.InjectToWorld(ref builder, profilingProvider, frameTimeCapBudget, memoryBudget, debugContainerBuilder);
        }
    }
}
