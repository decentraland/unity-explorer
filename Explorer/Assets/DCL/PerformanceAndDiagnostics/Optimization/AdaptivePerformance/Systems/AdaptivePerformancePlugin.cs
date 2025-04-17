using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.PluginSystem.Global;
using DCL.Profiling;
using DCL.RealmNavigation;
using System.Threading;
using UnityEngine;

namespace DCL.Optimization.AdaptivePerformance.Systems
{
    public class AdaptivePerformancePlugin : IDCLGlobalPlugin<AdaptivePerformanceSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly Profiler profiler;
        private readonly ILoadingStatus loadingStatus;

        private ProvidedAsset<AdaptivePhysicsSettings> physicsSettings;

        public AdaptivePerformancePlugin(IAssetsProvisioner assetsProvisioner, Profiler profiler, ILoadingStatus loadingStatus)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.profiler = profiler;
            this.loadingStatus = loadingStatus;
        }

        public async UniTask InitializeAsync(AdaptivePerformanceSettings settings, CancellationToken ct)
        {
            physicsSettings = await assetsProvisioner.ProvideMainAssetAsync(settings.phyiscsSettings, ct);

            Physics.simulationMode = physicsSettings.Value.Mode == PhysSimulationMode.MANUAL ? SimulationMode.Script : SimulationMode.FixedUpdate;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            UpdatePhysicsSimulationSystem.InjectToWorld(ref builder, profiler, physicsSettings.Value, loadingStatus);
            AdaptPhysicsSystem.InjectToWorld(ref builder, profiler, physicsSettings.Value, loadingStatus);
        }

        public void Dispose()
        {
        }
    }
}
