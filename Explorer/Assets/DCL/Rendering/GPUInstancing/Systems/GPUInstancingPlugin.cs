using Arch.Core;
using Arch.SystemGroups;
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
        private readonly GPUInstancingService gpuInstancingService;

        public GPUInstancingPlugin(GPUInstancingService gpuInstancingService, IRealmData realmData, ILoadingStatus loadingStatus)
        {
            this.realmData = realmData;
            this.loadingStatus = loadingStatus;
            this.gpuInstancingService = gpuInstancingService;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            GPUInstancingRenderSystem.InjectToWorld(ref builder, gpuInstancingService, realmData, loadingStatus);
        }

        public void Dispose()
        {
            gpuInstancingService.Dispose();
        }
    }
}
