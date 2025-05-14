using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.AssetsProvision;
using DCL.CharacterCamera;
using DCL.Landscape.Settings;
using DCL.PluginSystem.Global;
using DCL.RealmNavigation;
using ECS;
using System.Threading;

namespace DCL.Rendering.GPUInstancing.Systems
{
    public class GPUInstancingPlugin  : IDCLGlobalPlugin<LandscapeSettings>
    {
        private readonly IRealmData realmData;
        private readonly ILoadingStatus loadingStatus;
        private readonly ExposedCameraData exposedCameraData;
        private readonly GPUInstancingService gpuInstancingService;
        private readonly IAssetsProvisioner assetsProvisioner;

        public GPUInstancingPlugin(GPUInstancingService gpuInstancingService, IAssetsProvisioner assetsProvisioner, IRealmData realmData, ILoadingStatus loadingStatus, ExposedCameraData exposedCameraData)
        {
            this.realmData = realmData;
            this.loadingStatus = loadingStatus;
            this.exposedCameraData = exposedCameraData;
            this.gpuInstancingService = gpuInstancingService;
            this.assetsProvisioner = assetsProvisioner;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            //GPUInstancingRenderSystem.InjectToWorld(ref builder, gpuInstancingService, realmData, loadingStatus, exposedCameraData);
        }

        public void Dispose()
        {
            gpuInstancingService.Dispose();
        }

        public async UniTask InitializeAsync(LandscapeSettings settings, CancellationToken ct)
        {
            ProvidedAsset<LandscapeData> landscapeData = await assetsProvisioner.ProvideMainAssetAsync(settings.landscapeData, ct);
            gpuInstancingService.LandscapeData = landscapeData.Value;
        }
    }
}
