using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Roads.GPUInstancing;
using ECS;
using ECS.Abstract;

namespace DCL.Rendering.GPUInstancing.Systems
{
    [UpdateInGroup(typeof(PreRenderingSystemGroup))]
    [LogCategory(ReportCategory.GPU_INSTANCING)]
    public partial class GPUInstancingRenderSystem : BaseUnityLoopSystem
    {
        private readonly GPUInstancingService_Old gpuInstancingServiceOld;
        private readonly IRealmData realmData;

        public GPUInstancingRenderSystem(World world, GPUInstancingService_Old gpuInstancingServiceOld, IRealmData realmData) : base(world)
        {
            this.gpuInstancingServiceOld = gpuInstancingServiceOld;
            this.realmData = realmData;
        }

        protected override void Update(float t)
        {
            if (realmData.Configured)
                gpuInstancingServiceOld.RenderIndirect();
        }
    }
}
