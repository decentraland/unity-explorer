using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Diagnostics;
using DCL.Profiling;
using DCL.RealmNavigation;
using ECS.Abstract;
using UnityEngine;
using static DCL.Utilities.ConversionUtils;

namespace DCL.Optimization.AdaptivePerformance.Systems
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class AdaptPhysicsSystem : BaseUnityLoopSystem
    {
        private readonly Profiler profiler;
        private readonly AdaptivePhysicsSettings settings;
        private readonly ILoadingStatus loadingStatus;

        private float lastTimeChanged;

        public AdaptPhysicsSystem(World world, Profiler profiler, AdaptivePhysicsSettings settings, ILoadingStatus loadingStatus) : base(world)
        {
            this.profiler = profiler;
            this.settings = settings;
            this.loadingStatus = loadingStatus;
        }

        protected override void Update(float t)
        {
            ReportHub.Log(ReportCategory.ADAPTIVE_PERFORMANCE, $"Physics simulations in frame: {profiler.PhysicsSimulationInFrame} | FrameTime median: {profiler.MainThreadFrameTimes.Percentile(50) * NS_TO_SEC} | fixedDeltaTime: {UnityEngine.Time.fixedDeltaTime}");

            if (!settings.isEnabled || loadingStatus.CurrentStage != LoadingStatus.LoadingStage.Completed
                                    || UnityEngine.Time.unscaledTime - lastTimeChanged < settings.changeCooldown
                                    || profiler.MainThreadFrameTimes.SamplesAmount < settings.minFrameTimeAmount
               )
            {
                profiler.PhysicsSimulationInFrame = 0;
                return;
            }

            float medianFrameTime = profiler.MainThreadFrameTimes.Percentile(50) * NS_TO_SEC;

            if (medianFrameTime < UnityEngine.Time.fixedDeltaTime ||
                medianFrameTime > UnityEngine.Time.fixedDeltaTime + (settings.topOffset * MILISEC_TO_SEC))
            {
                float bufferedDeltaTime = medianFrameTime - (settings.bottomOffset * MILISEC_TO_SEC);
                UnityEngine.Time.fixedDeltaTime = Mathf.Clamp(bufferedDeltaTime, settings.minFixedDelta, settings.maxFixedDelta);
                lastTimeChanged = UnityEngine.Time.unscaledTime;
            }

            profiler.UpdatePhysicsSimRingBuffer();
            profiler.PhysicsSimulationInFrame = 0;
        }
    }
}
