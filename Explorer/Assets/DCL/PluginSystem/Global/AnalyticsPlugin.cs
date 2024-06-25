using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Analytics.Systems;
using DCL.AssetsProvision;
using DCL.PerformanceAndDiagnostics.Analytics;
using ECS;
using System;
using System.Threading;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DCL.PluginSystem.Global
{
    public class AnalyticsPlugin : IDCLGlobalPlugin<AnalyticsSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IRealmData realmData;

        private AnalyticsController analytics;

        public AnalyticsPlugin(IAssetsProvisioner assetsProvisioner, IRealmData realmData)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.realmData = realmData;
        }

        public async UniTask InitializeAsync(AnalyticsSettings settings, CancellationToken ct)
        {
            var analyticsConfig = await assetsProvisioner.ProvideMainAssetAsync(settings.AnalyticsConfigRef, ct);
            analytics = new AnalyticsController(
                // new DebugAnalyticsService()
                new SegmentAnalyticsService(analyticsConfig.Value)
                );
        }

        public void Dispose()
        {
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            PlayerParcelChangedAnalyticsSystem.InjectToWorld(ref builder, analytics, realmData, arguments.PlayerEntity);
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
