using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.Profiling;
using DCL.RealmNavigation;
using DCL.Utilities;
using ECS.Abstract;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Optimization.AdaptivePerformance.Systems
{
    [UpdateInGroup(typeof(PostRenderingSystemGroup))]
    public partial class AdaptPhysicsSystem : BaseUnityLoopSystem
    {
        private readonly Profiler profiler;
        private readonly AdaptivePhysicsSettings settings;
        private readonly ILoadingStatus loadingStatus;
        private readonly Queue<int> fpsBufferHigh = new ();
        private readonly Queue<int> fpsBufferLow = new ();

        public int fpsBufferSizeHigh = 60; // ~1 second if ~60 FPS

        public int fpsBufferSizeLow = 30; // ~1 second if ~60 FPS
        private readonly float avgTicksHigh = 0;
        private float avgTicksLow = 0;

        private float changedFrame;

        // Smoothed calls
        private float smoothedCalls;

        public AdaptPhysicsSystem(World world, Profiler profiler, AdaptivePhysicsSettings settings, ILoadingStatus loadingStatus) : base(world)
        {
            this.profiler = profiler;
            this.settings = settings;
            this.loadingStatus = loadingStatus;

            fpsBufferSizeHigh = this.settings.bufferSizeInFrames;
        }

        protected override void Update(float t)
        {
            if (loadingStatus.CurrentStage != LoadingStatus.LoadingStage.Completed) return;

            float currentDeltaTime = profiler.MainThreadRecorder.Percentile(50) * ConversionUtils.NS_TO_MS;

            // - 1 sec cooldown;
            // - check avg delta, too not change too often
            // - [Optional] handle teleportation case - maybe shorter frames span avg

            // if (UnityEngine.Time.unscaledTime - changedFrame > 3f)
            {
                UnityEngine.Time.fixedDeltaTime = (currentDeltaTime - 4)/1000f;
                // changedFrame = UnityEngine.Time.unscaledTime;
            }

            // if (fpsBufferHigh.Count >= fpsBufferSizeHigh)
            //     fpsBufferHigh.Dequeue();
            // fpsBufferHigh.Enqueue(currentTicks);
            // foreach (var fps in fpsBufferHigh)
            //     avgTicksHigh += fps > 1? 2 : fps ;
            // avgTicksHigh /= fpsBufferHigh.Count;
            //
            // if (fpsBufferLow.Count >= fpsBufferSizeLow)
            //     fpsBufferLow.Dequeue();
            // fpsBufferLow.Enqueue(currentTicks);
            // foreach (var fps in fpsBufferLow)
            //     avgTicksHigh += fps > 1? 2 : fps ;
            // avgTicksHigh /= fpsBufferLow.Count;
            //
            // smoothedCalls = (settings.alpha * Profiler.PhysTick) + ((1f - settings.alpha) * smoothedCalls);
            // AdjustPhysicsTime_Hybrid();
            // AdjustPhysicsTime_Smoothed();

            UnityEngine.Time.fixedDeltaTime = Mathf.Clamp(UnityEngine.Time.fixedDeltaTime, settings.minFixedDelta, settings.maxFixedDelta);

            // Debug.Log($"VVV {Profiler.PhysTick} {smoothedCalls} {avgTicksHigh} = {UnityEngine.Time.fixedDeltaTime}");
            Debug.Log($"VVV {Profiler.PhysTick} {currentDeltaTime} {UnityEngine.Time.fixedDeltaTime}");
            Profiler.PhysTick = 0;
        }

        private void AdjustPhysicsTime_Hybrid()
        {
            if (Profiler.PhysTick != 1)
            {
                if (Profiler.PhysTick == 0
                    && UnityEngine.Time.frameCount - changedFrame > 30
                    && smoothedCalls < settings.lowThreshold
                    && UnityEngine.Time.fixedDeltaTime > settings.minFixedDelta
                    && avgTicksHigh < settings.b_thresholdLow)
                {
                    UnityEngine.Time.fixedDeltaTime -= settings.smallDelta;
                    changedFrame = UnityEngine.Time.frameCount;
                    Debug.Log($"VVV CHANGED too rare {Profiler.PhysTick} {smoothedCalls} = {UnityEngine.Time.fixedDeltaTime} {UnityEngine.Time.frameCount}");
                }
                else if (Profiler.PhysTick > 1
                         && UnityEngine.Time.frameCount - changedFrame > 60
                         && smoothedCalls > settings.highThreshold
                         && UnityEngine.Time.fixedDeltaTime < settings.maxFixedDelta
                         && avgTicksHigh > settings.b_thresholdHigh)
                {
                    changedFrame = UnityEngine.Time.frameCount;
                    UnityEngine.Time.fixedDeltaTime += settings.bigDelta;
                    Debug.Log($"VVV CHANGED too often {Profiler.PhysTick} {smoothedCalls} = {UnityEngine.Time.fixedDeltaTime} {UnityEngine.Time.frameCount}");
                }
            }
        }

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
