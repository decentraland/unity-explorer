using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using static Unity.Mathematics.math;

namespace DCL.Profiling
{
    /// <summary>
    ///     Profiling provider to provide in game metrics. Profiler recorder returns values in NS, so to stay consistent with it,
    ///     our most used metric is going to be NS
    /// </summary>
    public class Profiler : IProfiler
    {
        private const int HICCUP_THRESHOLD_IN_NS = 50_000_000; // 50 ms ~ 20 FPS; also the lower floor for the target-relative threshold
        private const long NS_PER_SECOND = 1_000_000_000;
        private const float HICCUP_TARGET_FRAME_TIME_MULTIPLIER = 2f; // a frame counts as a hiccup above 2x the target frame time
        private const int FRAME_BUFFER_SIZE = 1_024; // 1000 samples: for 34 FPS it's 33 seconds gameplay, for 60 FPS it's 17 seconds
        private const int PHYS_SIM_BUFFER_SIZE = 10;

        private readonly List<ProfilerRecorderSample> samples = new (FRAME_BUFFER_SIZE);

        // Memory footprint of your application as seen by the operating system.
        private ProfilerRecorder systemUsedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
        private ProfilerRecorder totalUsedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");

#if ENABLE_PROFILER
        // Subtracted from memory readings in Editor/dev builds only: in release players these counters mirror "Total Used/Reserved Memory" instead of profiler overhead (Unity bug: https://discussions.unity.com/t/profiler-used-memory-on-non-development-player/900344), so subtracting them there would zero out the budget readings.
        private ProfilerRecorder profilerUsedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Profiler Used Memory");
        private ProfilerRecorder profilerReservedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Profiler Reserved Memory");
#endif
        private ProfilerRecorder gcUsedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory"); // Mono/IL2CPP heap size
        private ProfilerRecorder gcAllocatedInFrameRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

        private ProfilerRecorder mainThreadTimeRecorder = new (ProfilerCategory.Internal, "Main Thread", FRAME_BUFFER_SIZE); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
        private ProfilerRecorder gpuFrameTimeRecorder = new (ProfilerCategory.Render, "GPU Frame Time", FRAME_BUFFER_SIZE);

        private bool isCollectingFrameTimings;

        private readonly float[] physSimRingBuffer = new float[PHYS_SIM_BUFFER_SIZE];

        private int physSimBufferIndex;
        private float physSimRunningSum;

        public FrameTimesRecorder MainThreadFrameTimes { get; } = new (FRAME_BUFFER_SIZE);
        public FrameTimesRecorder GpuFrameTimes { get; } = new (FRAME_BUFFER_SIZE);

        public int PhysicsSimulationInFrame { get; set; }
        public float PhysicsSimulationsAvgInTenFrames => physSimRunningSum / PHYS_SIM_BUFFER_SIZE;

#if ENABLE_PROFILER
        public long TotalUsedMemoryInBytes => max(0L, totalUsedMemoryRecorder.CurrentValue - profilerUsedMemoryRecorder.CurrentValue);
        public long SystemUsedMemoryInBytes => max(0L, systemUsedMemoryRecorder.CurrentValue - profilerReservedMemoryRecorder.CurrentValue);
        public long ProfilerUsedMemoryInBytes => max(0L, profilerUsedMemoryRecorder.CurrentValue);
        public long ProfilerReservedMemoryInBytes => max(0L, profilerReservedMemoryRecorder.CurrentValue);
#else
        public long TotalUsedMemoryInBytes => totalUsedMemoryRecorder.CurrentValue;
        public long SystemUsedMemoryInBytes => systemUsedMemoryRecorder.CurrentValue;
        public long ProfilerUsedMemoryInBytes => 0L;
        public long ProfilerReservedMemoryInBytes => 0L;
#endif
        public long GcUsedMemoryInBytes => gcUsedMemoryRecorder.CurrentValue;
        public float TotalGcAlloc => GetRecorderSamplesSum(gcAllocatedInFrameRecorder);

        public ulong CurrentFrameTimeValueNs => (ulong)mainThreadTimeRecorder.CurrentValue;

        public ulong LastFrameTimeValueNs => (ulong)mainThreadTimeRecorder.LastValue;

        // On some hardware (low powered?), in some circumstances (high load?),
        // the GPU frame time can be slightly negative. It happens very rarely.
        public ulong LastGpuFrameTimeValueNs => (ulong)max(0L, gpuFrameTimeRecorder.LastValue);

        public ulong AllScenesTotalHeapSize { get; set; }
        public ulong AllScenesTotalHeapSizeExecutable { get; set; }
        public ulong AllScenesTotalPhysicalSize { get; set; }
        public ulong AllScenesUsedHeapSize { get; set; }
        public ulong AllScenesHeapSizeLimit { get; set; }
        public ulong AllScenesTotalExternalSize { get; set; }
        public int ActiveEngines { get; set; }
        public ulong CurrentSceneTotalHeapSize { get; set; }
        public ulong CurrentSceneTotalHeapSizeExecutable { get; set; }
        public ulong CurrentSceneUsedHeapSize { get; set; }
        public bool CurrentSceneHasStats { get; set; }

        public bool IsCollectingFrameData => mainThreadTimeRecorder.IsRunning;

        public void Dispose()
        {
            systemUsedMemoryRecorder.Dispose();
            totalUsedMemoryRecorder.Dispose();
#if ENABLE_PROFILER
            profilerUsedMemoryRecorder.Dispose();
            profilerReservedMemoryRecorder.Dispose();
#endif
            gcUsedMemoryRecorder.Dispose();
            gcAllocatedInFrameRecorder.Dispose();

            mainThreadTimeRecorder.Dispose();
            gpuFrameTimeRecorder.Dispose();
        }

        public void StopFrameTimeDataCollection()
        {
            if (mainThreadTimeRecorder.IsRunning)
            {
                mainThreadTimeRecorder.Stop();
                gpuFrameTimeRecorder.Stop();
            }

            // Discard buffered samples so pre-transition hiccups aren't attributed to the next realm/scene on resume.
            ResetHiccupRecorders();
        }

        public void StartFrameTimeDataCollection()
        {
            if (!mainThreadTimeRecorder.IsRunning)
            {
                mainThreadTimeRecorder.Start();
                gpuFrameTimeRecorder.Start();
            }
        }

        public void UpdatePhysicsSimRingBuffer()
        {
            float oldValue = physSimRingBuffer[physSimBufferIndex];
            physSimRunningSum -= oldValue;

            physSimRingBuffer[physSimBufferIndex] = PhysicsSimulationInFrame;
            physSimRunningSum += PhysicsSimulationInFrame;

            physSimBufferIndex = (physSimBufferIndex + 1) % PHYS_SIM_BUFFER_SIZE;
        }

        public void UpdateFrameTimings()
        {
            MainThreadFrameTimes.AddFrameTime(LastFrameTimeValueNs);
            GpuFrameTimes.AddFrameTime(LastGpuFrameTimeValueNs);
        }

        public void ClearFrameTimings()
        {
            MainThreadFrameTimes.Clear();
            GpuFrameTimes.Clear();

            // Reset the hiccup buffers too, so each report counts only its own interval's frames.
            ResetHiccupRecorders();
        }

        /// <summary>
        ///     In nanoseconds
        /// </summary>
        public FrameTimeStats? CalculateMainThreadFrameTimesNs()
        {
            samples.Clear();
            mainThreadTimeRecorder.CopyTo(samples);

            if (samples.Count == 0)
                return null;

            long minFrameTime = long.MaxValue;
            long maxFrameTime = long.MinValue;

            long hiccupCount = 0;
            long thresholdNs = EffectiveHiccupThresholdNs();

            for (var i = 0; i < samples.Count; i++)
            {
                long frameTime = samples[i].Value;

                if (frameTime > thresholdNs) hiccupCount++;
                if (frameTime < minFrameTime) minFrameTime = frameTime;
                if (frameTime > maxFrameTime) maxFrameTime = frameTime;
            }

            return new FrameTimeStats(minFrameTime, maxFrameTime, hiccupCount);
        }

        public HiccupStats CalculateMainThreadHiccups() =>
            CalculateThreadHiccups(mainThreadTimeRecorder);

        public HiccupStats CalculateGpuHiccups() =>
            CalculateThreadHiccups(gpuFrameTimeRecorder);

        private HiccupStats CalculateThreadHiccups(ProfilerRecorder recorder)
        {
            samples.Clear();
            recorder.CopyTo(samples);

            // Actual frames in the window; below FRAME_BUFFER_SIZE during warm-up or right after a reset.
            int sampleCount = samples.Count;
            long thresholdNs = EffectiveHiccupThresholdNs();

            if (sampleCount == 0)
                return new HiccupStats(false, 0, 0, 0, 0, 0, 0, 0, thresholdNs);

            long hiccupCount = 0;
            long hiccupTotalTime = 0;
            long hiccupExcessTime = 0;
            long hiccupMin = -1;
            long hiccupMax = -1;

            for (var i = 0; i < sampleCount; i++)
            {
                long frameTime = samples[i].Value;

                if (frameTime > thresholdNs)
                {
                    hiccupCount++;
                    hiccupTotalTime += frameTime;
                    hiccupExcessTime += frameTime - thresholdNs;

                    if (frameTime > hiccupMax) hiccupMax = frameTime;

                    if (hiccupMin == -1) hiccupMin = frameTime;
                    else if (frameTime < hiccupMin) hiccupMin = frameTime;
                }
            }

            float avg = hiccupCount == 0 ? 0 : hiccupTotalTime / (float)hiccupCount;
            return new HiccupStats(true, hiccupCount, hiccupTotalTime, hiccupExcessTime, hiccupMin, hiccupMax, avg, sampleCount, thresholdNs);
        }

        // Hiccup bar = 2x the target frame time, floored at 50 ms. Uncapped/vsync configs (>= 60 FPS)
        // stay at the floor; capped configs (e.g. 30 FPS -> 67 ms) get a higher bar so their normal
        // cadence isn't counted as hiccups.
        private static long EffectiveHiccupThresholdNs()
        {
            int targetFps = Application.targetFrameRate;

            // 0 (vsync) or -1 (uncapped): no cap set, so use the floor.
            if (targetFps <= 0)
                return HICCUP_THRESHOLD_IN_NS;

            var relativeThresholdNs = (long)(HICCUP_TARGET_FRAME_TIME_MULTIPLIER * NS_PER_SECOND / targetFps);
            return max(HICCUP_THRESHOLD_IN_NS, relativeThresholdNs);
        }

        private float GetRecorderSamplesSum(ProfilerRecorder recorder)
        {
            samples.Clear();
            recorder.CopyTo(samples);

            if (samples.Count == 0)
                return 0;

            float r = 0;

            for (var i = 0; i < samples.Count; i++)
                r += samples[i].Value;

            return r;
        }

        // ProfilerRecorder has no flush, so a fresh window means recreating the recorders (keeping run state).
        private void ResetHiccupRecorders()
        {
            bool wasRunning = mainThreadTimeRecorder.IsRunning;

            mainThreadTimeRecorder.Dispose();
            gpuFrameTimeRecorder.Dispose();

            mainThreadTimeRecorder = new ProfilerRecorder(ProfilerCategory.Internal, "Main Thread", FRAME_BUFFER_SIZE); // IGNORE_LINE_WEBGL_THREAD_SAFETY_FLAG
            gpuFrameTimeRecorder = new ProfilerRecorder(ProfilerCategory.Render, "GPU Frame Time", FRAME_BUFFER_SIZE);

            if (wasRunning)
            {
                mainThreadTimeRecorder.Start();
                gpuFrameTimeRecorder.Start();
            }
        }
    }
}
