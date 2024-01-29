using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.DebugUtilities;
using DCL.Landscape.Settings;
using DCL.Landscape.Systems;
using ECS.Prioritization;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class LandscapeDebugPlugin : IDCLGlobalPlugin<LandscapeDebugSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private readonly IDebugContainerBuilder debugContainerBuilder;
        private ProvidedAsset<RealmPartitionSettingsAsset> realmPartitionSettings;
        private ProvidedAsset<LandscapeData> landscapeDataRef;

        public LandscapeDebugPlugin(IAssetsProvisioner assetsProvisioner, IDebugContainerBuilder debugContainerBuilder)
        {
            this.assetsProvisioner = assetsProvisioner;
            this.debugContainerBuilder = debugContainerBuilder;
        }

        public async UniTask InitializeAsync(LandscapeDebugSettings settings, CancellationToken ct)
        {
            realmPartitionSettings = await assetsProvisioner.ProvideMainAssetAsync(settings.realmPartitionSettings, ct);
            landscapeDataRef = await assetsProvisioner.ProvideMainAssetAsync(settings.landscapeData, ct);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            LandscapeDebugSystem.InjectToWorld(ref builder, debugContainerBuilder, realmPartitionSettings.Value, landscapeDataRef.Value);
        }

        public void Dispose() { }
    }
}
