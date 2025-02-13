using Arch.Core;
using Arch.SystemGroups;
using DCL.PluginSystem.Global;
using DCL.Roads.GPUInstancing;
using ECS;

namespace DCL.Rendering.GPUInstancing.Systems
{
    public class GPUInstancingPlugin  : IDCLGlobalPlugin
    {
        private readonly IRealmData realmData;
        private readonly GPUInstancingService_Old gpuInstancingServiceOld;

        public GPUInstancingPlugin(GPUInstancingService_Old gpuInstancingServiceOld, IRealmData realmData)
        {
            this.realmData = realmData;
            this.gpuInstancingServiceOld = gpuInstancingServiceOld;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            GPUInstancingRenderSystem.InjectToWorld(ref builder, gpuInstancingServiceOld, realmData);
        }

        public void Dispose()
        {
            gpuInstancingServiceOld.Dispose();
        }
    }
}
