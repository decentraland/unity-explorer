using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.DebugUtilities;
using DCL.Optimization.AdaptivePerformance.Systems;
using DCL.Optimization.PerformanceBudgeting;
using DCL.PerformanceAndDiagnostics.AutoPilot;
using DCL.Profiling;
using DCL.Profiling.ECS;
using DCL.RealmNavigation;
using ECS;
using ECS.SceneLifeCycle;
using ECS.SceneLifeCycle.IncreasingRadius;
using Global.AppArgs;
using Global.Versioning;

namespace DCL.PluginSystem.Global
{
    public class ProfilingPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IProfiler profiler;
        private readonly IRealmData realmData;
        private readonly MemoryBudget memoryBudget;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private readonly IScenesCache scenesCache;
        private readonly DCLVersion dclVersion;
        private readonly AdaptivePhysicsSettings adaptivePhysicsSettings;
        private readonly SceneLoadingLimit sceneLoadingLimit;
        private readonly IAppArgs appArgs;
        private readonly ILoadingStatus loadingStatus;

        public ProfilingPlugin(IProfiler profiler, IRealmData realmData, MemoryBudget memoryBudget,
            IDebugContainerBuilder debugContainerBuilder, IScenesCache scenesCache, DCLVersion dclVersion,
            AdaptivePhysicsSettings adaptivePhysicsSettings, SceneLoadingLimit sceneLoadingLimit,
            IAppArgs appArgs, ILoadingStatus loadingStatus)
        {
            this.profiler = profiler;
            this.realmData = realmData;
            this.debugContainerBuilder = debugContainerBuilder;
            this.scenesCache = scenesCache;
            this.dclVersion = dclVersion;
            this.adaptivePhysicsSettings = adaptivePhysicsSettings;
            this.sceneLoadingLimit = sceneLoadingLimit;
            this.memoryBudget = memoryBudget;
            this.appArgs = appArgs;
            this.loadingStatus = loadingStatus;
        }

        public void Dispose()
        {
            profiler.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            UpdateProfilerSystem.InjectToWorld(ref builder, profiler, scenesCache);

            DebugViewProfilingSystem.InjectToWorld(ref builder, realmData, profiler, memoryBudget,
                debugContainerBuilder, dclVersion, adaptivePhysicsSettings, sceneLoadingLimit);

            if (appArgs.HasFlag(AppArgsFlags.AUTOPILOT))
            {
                var autoPilot = new AutoPilot(appArgs, loadingStatus, profiler);
                autoPilot.RunAsync().Forget();
            }
        }
    }
}
