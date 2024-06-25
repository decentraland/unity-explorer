using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.PluginSystem.World;
using DCL.PluginSystem.World.Dependencies;
using ECS.LifeCycle;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class AnalyticsPlugin : IDCLWorldPlugin<AnalyticsSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;

        private AnalyticsController analytics;

        public AnalyticsPlugin(IAssetsProvisioner assetsProvisioner)
        {
            this.assetsProvisioner = assetsProvisioner;
        }

        public async UniTask InitializeAsync(AnalyticsSettings settings, CancellationToken ct)
        {
            var analyticsConfig = await assetsProvisioner.ProvideMainAssetAsync(settings.AnalyticsConfigRef, ct);
            analytics = new AnalyticsController(
                new SegmentAnalyticsService(analyticsConfig.Value)
                );
        }

        public void Dispose()
        {
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in ECSWorldInstanceSharedDependencies sharedDependencies, in PersistentEntities persistentEntities, List<IFinalizeWorldSystem> finalizeWorldSystems, List<ISceneIsCurrentListener> sceneIsCurrentListeners)
        {
        }
    }

    [Serializable]
    public class AnalyticsSettings : IDCLPluginSettings
    {
        [field: SerializeField] public AnalyticsConfigurationRef AnalyticsConfigRef;

        [Serializable]
        public class AnalyticsConfigurationRef : AssetReferenceT<AnalyticsConfiguration>
        {
            public AnalyticsConfigurationRef(string guid) : base(guid) { }
        }
    }
}
