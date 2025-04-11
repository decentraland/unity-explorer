using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Profiling;
using DCL.RealmNavigation;
using ECS.Abstract;

namespace DCL.Optimization.AdaptivePerformance.Systems
{
    [UpdateInGroup(typeof(PostPhysicsSystemGroup))]
    public partial class UpdatePhysicsProfilerSystem : BaseUnityLoopSystem
    {
        private readonly Profiler profiler;
        private readonly ILoadingStatus loadingStatus;

        public UpdatePhysicsProfilerSystem(World _, Profiler profiler, ILoadingStatus loadingStatus) : base(_)
        {
            this.profiler = profiler;
            this.loadingStatus = loadingStatus;
        }

        protected override void Update(float t)
        {
            if (loadingStatus.CurrentStage == LoadingStatus.LoadingStage.Completed)
                profiler.PhysicsSimulationInFrame++;
        }
    }
}
