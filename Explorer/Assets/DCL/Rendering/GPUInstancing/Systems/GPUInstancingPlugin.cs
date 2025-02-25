using Arch.Core;
using Arch.SystemGroups;
using DCL.CharacterCamera;
using DCL.PluginSystem.Global;
using DCL.RealmNavigation;
using DCL.Roads.GPUInstancing;
using ECS;

namespace DCL.Rendering.GPUInstancing.Systems
{
    public class GPUInstancingPlugin  : IDCLGlobalPlugin
    {
        private readonly IRealmData realmData;
        private readonly ILoadingStatus loadingStatus;
        private readonly ExposedCameraData exposedCameraData;
        private readonly GPUInstancingService gpuInstancingService;

        public GPUInstancingPlugin(GPUInstancingService gpuInstancingService, IRealmData realmData, ILoadingStatus loadingStatus, ExposedCameraData exposedCameraData)
        {
            this.realmData = realmData;
            this.loadingStatus = loadingStatus;
            this.exposedCameraData = exposedCameraData;
            this.gpuInstancingService = gpuInstancingService;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            GPUInstancingRenderSystem.InjectToWorld(ref builder, gpuInstancingService, realmData, loadingStatus, exposedCameraData);
        }

        public void Dispose()
        {
            gpuInstancingService.Dispose();
        }
    }
}
