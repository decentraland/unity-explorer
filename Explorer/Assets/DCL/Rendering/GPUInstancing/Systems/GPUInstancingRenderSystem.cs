using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.CharacterCamera;
using DCL.Diagnostics;
using DCL.RealmNavigation;
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
        private readonly ExposedCameraData exposedCameraData;

        private bool isCameraCached;

        public GPUInstancingRenderSystem(World world,
            GPUInstancingService gpuInstancingService,
            IRealmData realmData,
            ILoadingStatus loadingStatus,
            ExposedCameraData exposedCameraData) : base(world)
        {
            this.gpuInstancingService = gpuInstancingService;
            this.realmData = realmData;
            this.loadingStatus = loadingStatus;
            this.exposedCameraData = exposedCameraData;
        }

        protected override void Update(float t)
        {
            if (!isCameraCached && exposedCameraData.CinemachineBrain.OutputCamera != null)
            {
                gpuInstancingService.SetCamera(exposedCameraData.CinemachineBrain.OutputCamera);
                isCameraCached = true;
            }

            if (!gpuInstancingService.IsEnabled) return;

            if (isCameraCached && loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed && realmData.Configured)
                gpuInstancingService.RenderIndirect();
        }
    }
}
