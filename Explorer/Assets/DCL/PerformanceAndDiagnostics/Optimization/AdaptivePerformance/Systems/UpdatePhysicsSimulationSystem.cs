using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Profiling;
using DCL.RealmNavigation;
using ECS.Abstract;
using UnityEngine;

namespace DCL.Optimization.AdaptivePerformance.Systems
{
    [UpdateInGroup(typeof(PhysicsSystemGroup))]
    public partial class UpdatePhysicsSimulationSystem : BaseUnityLoopSystem
    {
        private readonly AdaptivePhysicsSettings settings;
        private readonly Profiler profiler;
        private readonly ILoadingStatus loadingStatus;

        public UpdatePhysicsSimulationSystem(World _, Profiler profiler, AdaptivePhysicsSettings settings, ILoadingStatus loadingStatus) : base(_)
        {
            this.settings = settings;
            this.profiler = profiler;
            this.loadingStatus = loadingStatus;
        }

        protected override void Update(float t)
        {
            switch (settings.Mode)
            {
                case PhysSimulationMode.Manual:
                {
                    if (profiler.PhysicsSimulationInFrame == 0)
                    {
                        Physics.Simulate(UnityEngine.Time.deltaTime);
                        profiler.PhysicsSimulationInFrame++;
                    }

                    break;
                }
                case PhysSimulationMode.Default:
                case PhysSimulationMode.Adaptive:
                default:
                {
                    if (loadingStatus.CurrentStage.Value == LoadingStatus.LoadingStage.Completed)
                        profiler.PhysicsSimulationInFrame++;

                    break;
                }
            }
        }
    }
}
