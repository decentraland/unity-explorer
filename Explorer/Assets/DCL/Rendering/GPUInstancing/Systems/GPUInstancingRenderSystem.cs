using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.RealmNavigation;
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
        private readonly ILoadingStatus loadingStatus;

        public GPUInstancingRenderSystem(World world, GPUInstancingService gpuInstancingService, IRealmData realmData, ILoadingStatus loadingStatus) : base(world)
        {
            this.gpuInstancingService = gpuInstancingService;
            this.realmData = realmData;
            this.loadingStatus = loadingStatus;
        }

        protected override void Update(float t)
        {
            if (loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed && realmData.Configured)
                gpuInstancingService.RenderIndirect();
        }
    }
}
