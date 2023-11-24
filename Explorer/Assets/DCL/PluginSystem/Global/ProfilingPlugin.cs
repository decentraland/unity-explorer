using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.PerformanceAndDiagnostics.Optimization.PerformanceBudgeting;
using DCL.Profiling;
using DCL.Profiling.ECS;
using System;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class ProfilingPlugin : IDCLGlobalPlugin<ProfilingPlugin.Settings>
    {
        private readonly IProfilingProvider profilingProvider;
        private readonly FrameTimeCapBudgetProvider frameTimeCapBudgetProvider;
        private readonly MemoryBudgetProvider memoryBudgetProvider;
        private readonly IDebugContainerBuilder debugContainerBuilder;

        public ProfilingPlugin(IProfilingProvider profilingProvider, FrameTimeCapBudgetProvider frameTimeCapBudgetProvider, MemoryBudgetProvider memoryBudgetProvider, IDebugContainerBuilder debugContainerBuilder)
        {
            this.profilingProvider = profilingProvider;
            this.frameTimeCapBudgetProvider = frameTimeCapBudgetProvider;
            this.debugContainerBuilder = debugContainerBuilder;
            this.memoryBudgetProvider = memoryBudgetProvider;
        }

        public void Dispose() { }

        public UniTask InitializeAsync(Settings settings, CancellationToken ct) =>
            UniTask.CompletedTask;

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            ProfilingSystem.InjectToWorld(ref builder, profilingProvider, frameTimeCapBudgetProvider, memoryBudgetProvider, debugContainerBuilder);
        }

        [Serializable]
        public class Settings : IDCLPluginSettings { }
    }
}
