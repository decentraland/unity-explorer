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
        private readonly GPUInstancingService gpuInstancingService;
        private readonly IRealmData realmData;

        public GPUInstancingRenderSystem(World world, GPUInstancingService gpuInstancingService, IRealmData realmData) : base(world)
        {
            this.gpuInstancingService = gpuInstancingService;
            this.realmData = realmData;
        }

        protected override void Update(float t)
        {
            if (realmData.Configured)
                gpuInstancingService.RenderIndirect();
        }
    }
}
