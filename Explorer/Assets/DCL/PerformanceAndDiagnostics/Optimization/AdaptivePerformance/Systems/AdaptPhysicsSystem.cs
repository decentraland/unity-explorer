using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Profiling;
using DCL.RealmNavigation;
using ECS.Abstract;
using UnityEngine;

namespace DCL.Optimization.AdaptivePerformance.Systems
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class AdaptPhysicsSystem : BaseUnityLoopSystem
    {
        private readonly Profiler profiler;
        private readonly AdaptivePhysicsSettings settings;
        private readonly ILoadingStatus loadingStatus;

        public AdaptPhysicsSystem(World world, Profiler profiler, AdaptivePhysicsSettings settings, ILoadingStatus loadingStatus) : base(world)
        {
            this.profiler = profiler;
            this.settings = settings;
            this.loadingStatus = loadingStatus;
        }

        protected override void Update(float t)
        {
            if(loadingStatus.CurrentStage != LoadingStatus.LoadingStage.Completed || profiler.LastFrameTimeValueNs >= profiler.SpikeFrameTime) return;

            smoothedCalls = (settings.alpha * Profiler.PhysTick) + ((1f - settings.alpha) * smoothedCalls);

            AdjustPhysicsTime_Hybrid();
            // AdjustPhysicsTime_Smoothed();

            UnityEngine.Time.fixedDeltaTime = Mathf.Clamp(UnityEngine.Time.fixedDeltaTime, settings.minFixedDelta, settings.maxFixedDelta);
            Debug.Log($"VVV {Profiler.PhysTick} {smoothedCalls} = {UnityEngine.Time.fixedDeltaTime}");
            Profiler.PhysTick = 0;
        }

        private bool isChanged;
        private void AdjustPhysicsTime_Hybrid()
        {
            if (Profiler.PhysTick != 1)
            {
                if (Profiler.PhysTick == 0 && smoothedCalls < settings.lowThreshold && UnityEngine.Time.fixedDeltaTime > settings.minFixedDelta)
                {
                    UnityEngine.Time.fixedDeltaTime -= settings.smallDelta;
                    Debug.Log($"VVV CHANGED too rare {Profiler.PhysTick} {smoothedCalls} = {UnityEngine.Time.fixedDeltaTime} {UnityEngine.Time.frameCount}");
                }
                else if (Profiler.PhysTick > 1 && smoothedCalls > settings.highThreshold && UnityEngine.Time.fixedDeltaTime < settings.maxFixedDelta)
                {
                    UnityEngine.Time.fixedDeltaTime += settings.bigDelta;
                    Debug.Log($"VVV CHANGED too often {Profiler.PhysTick} {smoothedCalls} = {UnityEngine.Time.fixedDeltaTime} {UnityEngine.Time.frameCount}");
                }
            }
        }

        // Smoothed calls
        private float smoothedCalls = 0f;
        private void AdjustPhysicsTime_Smoothed()
        {
            smoothedCalls = (settings.alpha * Profiler.PhysTick) + ((1f - settings.alpha) * smoothedCalls);

            if (smoothedCalls > settings.highThreshold)
            {
                if (UnityEngine.Time.fixedDeltaTime < settings.maxFixedDelta)
                {
                    UnityEngine.Time.fixedDeltaTime += settings.smallDelta;
                    Debug.Log($"VVV CHANGED too often {UnityEngine.Time.frameCount}");
                }
            }
            else if (smoothedCalls < settings.lowThreshold)
            {
                if (UnityEngine.Time.fixedDeltaTime > settings.minFixedDelta)
                {
                    UnityEngine.Time.fixedDeltaTime -= settings.smallDelta;
                    Debug.Log($"VVV CHANGED too rare {UnityEngine.Time.frameCount}");
                }
            }
        }
    }
}
