using System.Collections.Generic;
using Unity.Profiling;

namespace DCL.Profiling
{
    /// <summary>
    ///     Profiling provider to provide in game metrics. Profiler recorder returns values in NS, so to stay consistent with it,
    ///     our most used metric is going to be NS
    /// </summary>
    public class Profiler : IProfiler
    {
        private const int HICCUP_THRESHOLD_IN_NS = 50_000_000; // 50 ms ~ 20 FPS
        private const int FRAME_BUFFER_SIZE = 1_000; // 1000 samples: for 30 FPS it's 33 seconds gameplay, for 60 FPS it's 16.6 seconds

        private readonly List<ProfilerRecorderSample> samples = new (FRAME_BUFFER_SIZE);

        // Memory footprint of your application as seen by the operating system.
        private ProfilerRecorder systemUsedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
        private ProfilerRecorder totalUsedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
        private ProfilerRecorder gcUsedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory"); // Mono/IL2CPP heap size
        private ProfilerRecorder gcAllocatedInFrameRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

        private ProfilerRecorder mainThreadTimeRecorder = new (ProfilerCategory.Internal, "Main Thread", FRAME_BUFFER_SIZE);
        private ProfilerRecorder gpuFrameTimeRecorder = new (ProfilerCategory.Render, "GPU Frame Time", FRAME_BUFFER_SIZE);

        private bool isCollectingFrameTimings;

        public long TotalUsedMemoryInBytes => totalUsedMemoryRecorder.CurrentValue;
        public long SystemUsedMemoryInBytes => systemUsedMemoryRecorder.CurrentValue;
        public long GcUsedMemoryInBytes => gcUsedMemoryRecorder.CurrentValue;
        public float TotalGcAlloc => GetRecorderSamplesSum(gcAllocatedInFrameRecorder);

        public ulong CurrentFrameTimeValueNs => (ulong)mainThreadTimeRecorder.CurrentValue;

        public ulong LastFrameTimeValueNs => (ulong)mainThreadTimeRecorder.LastValue;
        public ulong LastGpuFrameTimeValueNs => (ulong)gpuFrameTimeRecorder.LastValue;

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
        }

        public void StartFrameTimeDataCollection()
        {
            if (!mainThreadTimeRecorder.IsRunning)
            {
                mainThreadTimeRecorder.Start();
                gpuFrameTimeRecorder.Start();
            }
        }

        /// <summary>
        ///     In nanoseconds
        /// </summary>
        public FrameTimeStats? CalculateMainThreadFrameTimesNs()
        {
            int availableSamples = mainThreadTimeRecorder.Capacity;

            if (availableSamples == 0)
                return null;

            long minFrameTime = long.MaxValue;
            long maxFrameTime = long.MinValue;

            long hiccupCount = 0;

            samples.Clear();
            mainThreadTimeRecorder.CopyTo(samples);

            for (var i = 0; i < samples.Count; i++)
            {
                long frameTime = samples[i].Value;

                if (frameTime > HICCUP_THRESHOLD_IN_NS) hiccupCount++;
                if (frameTime < minFrameTime) minFrameTime = frameTime;
                if (frameTime > maxFrameTime) maxFrameTime = frameTime;
            }

            return new FrameTimeStats(minFrameTime, maxFrameTime, hiccupCount);
        }

        public (bool hasValue, long count, long sumTime, long min, long max, float avg) CalculateMainThreadHiccups() =>
            CalculateThreadHiccups(mainThreadTimeRecorder);

        public (bool hasValue, long count, long sumTime, long min, long max, float avg) CalculateGpuHiccups() =>
            CalculateThreadHiccups(gpuFrameTimeRecorder);

        private (bool hasValue, long count, long sumTime, long min, long max, float avg) CalculateThreadHiccups(ProfilerRecorder recorder)
        {
            int availableSamples = recorder.Capacity;

            if (availableSamples == 0)
                return (false, 0, 0, 0, 0, 0);

            long hiccupCount = 0;
            long hiccupTotalTime = 0;
            long hiccupMin = -1;
            long hiccupMax = -1;

            samples.Clear();
            recorder.CopyTo(samples);

            for (var i = 0; i < samples.Count; i++)
            {
                long frameTime = samples[i].Value;

                if (frameTime > HICCUP_THRESHOLD_IN_NS)
                {
                    hiccupCount++;
                    hiccupTotalTime += frameTime;

                    if (frameTime > hiccupMax) hiccupMax = frameTime;

                    if (hiccupMin == -1) hiccupMin = frameTime;
                    else if (frameTime < hiccupMin) hiccupMin = frameTime;
                }
            }

            return (true, hiccupCount, hiccupTotalTime, hiccupMin, hiccupMax, hiccupCount == 0 ? 0 : hiccupTotalTime / (float)hiccupCount);
        }

        private float GetRecorderSamplesSum(ProfilerRecorder recorder)
        {
            int samplesCount = recorder.Capacity;

            if (samplesCount == 0)
                return 0;

            float r = 0;

            samples.Clear();
            recorder.CopyTo(samples);

            for (var i = 0; i < samples.Count; i++)
                r += samples[i].Value;

            return r;
        }
    }

#if ENABLE_PROFILER
    public static class JavaScriptProfilerCounters
    {
        public const string CATEGORY_NAME = "JavaScript";
        public static readonly ProfilerCategory CATEGORY = new (CATEGORY_NAME);

        public static readonly string TOTAL_HEAP_SIZE_NAME = "Total Heap Size";
        public static readonly string TOTAL_HEAP_SIZE_EXECUTABLE_NAME = "Total Executable Heap Size";
        public static readonly string TOTAL_PHYSICAL_SIZE_NAME = "Total Physical Memory Size";
        public static readonly string USED_HEAP_SIZE_NAME = "Used Heap Size";
        public static readonly string TOTAL_EXTERNAL_SIZE_NAME = "Total External Memory Size";
        public static readonly string ACTIVE_ENGINES_NAME = "Active Engines";

        public static readonly ProfilerCounter<ulong> TOTAL_HEAP_SIZE
            = new (CATEGORY, TOTAL_HEAP_SIZE_NAME, ProfilerMarkerDataUnit.Bytes);

        public static readonly ProfilerCounter<ulong> TOTAL_HEAP_SIZE_EXECUTABLE
            = new (CATEGORY, TOTAL_HEAP_SIZE_EXECUTABLE_NAME, ProfilerMarkerDataUnit.Bytes);

        public static readonly ProfilerCounter<ulong> TOTAL_PHYSICAL_SIZE
            = new (CATEGORY, TOTAL_PHYSICAL_SIZE_NAME, ProfilerMarkerDataUnit.Bytes);

        public static readonly ProfilerCounter<ulong> USED_HEAP_SIZE
            = new (CATEGORY, USED_HEAP_SIZE_NAME, ProfilerMarkerDataUnit.Bytes);

        public static readonly ProfilerCounter<ulong> TOTAL_EXTERNAL_SIZE
            = new (CATEGORY, TOTAL_EXTERNAL_SIZE_NAME, ProfilerMarkerDataUnit.Bytes);

        public static readonly ProfilerCounter<int> ACTIVE_ENGINES
            = new (CATEGORY, ACTIVE_ENGINES_NAME, ProfilerMarkerDataUnit.Count);
    }
#endif
}
