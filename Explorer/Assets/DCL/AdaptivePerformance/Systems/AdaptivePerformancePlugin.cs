using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.PluginSystem.Global;
using DCL.Profiling;
using DCL.RealmNavigation;
using System.Threading;
using UnityEngine;
using UnityEngine.AdaptivePerformance;

namespace DCL.Optimization.AdaptivePerformance.Systems
{
    public class AdaptivePerformancePlugin : IDCLGlobalPlugin<AdaptivePerformanceSettings>
    {
        private readonly Profiler profiler;
        private readonly ILoadingStatus loadingStatus;

        private AdaptivePhysicsSettings physicsSettings;

        private IAdaptivePerformance adaptivePerformance;

        public AdaptivePerformancePlugin(Profiler profiler, ILoadingStatus loadingStatus)
        {
            this.profiler = profiler;
            this.loadingStatus = loadingStatus;
        }

        public UniTask InitializeAsync(AdaptivePerformanceSettings settings, CancellationToken ct)
        {
            // Adaptive Performance
            Holder.Initialize();

            // Physics
            physicsSettings = settings.phyiscsSettings;
            Physics.simulationMode = physicsSettings.Mode == PhysSimulationMode.MANUAL ? SimulationMode.Script : SimulationMode.FixedUpdate;

            return UniTask.CompletedTask;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            UpdatePhysicsSimulationSystem.InjectToWorld(ref builder, profiler, physicsSettings, loadingStatus);
            AdaptPhysicsSystem.InjectToWorld(ref builder, profiler, physicsSettings, loadingStatus);
        }

        public void Dispose() { }
    }
}
