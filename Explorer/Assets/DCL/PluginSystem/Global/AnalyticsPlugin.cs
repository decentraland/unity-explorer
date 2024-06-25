using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.Analytics.Systems;
using DCL.AssetsProvision;
using DCL.Character;
using DCL.PerformanceAndDiagnostics.Analytics;
using DCL.Web3.Identities;
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
        private readonly ICharacterObject characterObject;
        private readonly IWeb3IdentityCache identityCache;

        private AnalyticsController analytics;

        public AnalyticsPlugin(IAssetsProvisioner assetsProvisioner, IRealmData realmData, ICharacterObject characterObject, IWeb3IdentityCache identityCache)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.realmData = realmData;
            this.characterObject = characterObject;
            this.identityCache = identityCache;
        }

        public async UniTask InitializeAsync(AnalyticsSettings settings, CancellationToken ct)
        {
            var analyticsConfig = await assetsProvisioner.ProvideMainAssetAsync(settings.AnalyticsConfigRef, ct);
            analytics = new AnalyticsController(
                new DebugAnalyticsService(),
                // new SegmentAnalyticsService(analyticsConfig.Value),
                realmData, characterObject.Transform, identityCache
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
