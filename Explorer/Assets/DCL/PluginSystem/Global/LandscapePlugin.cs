using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.Landscape.Settings;
using System.Threading;

namespace DCL.PluginSystem.Global
{
    public class LandscapePlugin : IDCLGlobalPlugin<LandscapeSettings>
    {
        private readonly IAssetsProvisioner assetsProvisioner;
        private ProvidedAsset<LandscapeData> landscapeData;

        public LandscapePlugin(IAssetsProvisioner assetsProvisioner)
        {
            this.assetsProvisioner = assetsProvisioner;
        }

        public async UniTask InitializeAsync(LandscapeSettings settings, CancellationToken ct)
        {
            landscapeData = await assetsProvisioner.ProvideMainAssetAsync(settings.landscapeData, ct);
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            // Do we need the actual world or we can use a parallel one?
            Arch.Core.World world = builder.World;

            // add required entities

            // add reqiored systems
        }

        public void Dispose()
        {
            landscapeData.Dispose();
        }
    }
}
