using System.Collections.Generic;
using Unity.Profiling;

namespace DCL.Profiling
{
    /// <summary>
    ///     Profiling provider to provide in game metrics. Profiler recorder returns values in NS, so to stay consistent with it,
    ///     our most used metric is going to be NS
    /// </summary>
    public class Profiler : IDebugViewProfiler
    {
        private const int HICCUP_THRESHOLD_IN_NS = 50_000_000; // 50 ms ~ 20 FPS
        private const int FRAME_BUFFER_SIZE = 1_000; // 1000 samples: for 30 FPS it's 33 seconds gameplay, for 60 FPS it's 16.6 seconds

        private readonly List<ProfilerRecorderSample> samples = new (FRAME_BUFFER_SIZE);

        // Memory footprint of your application as seen by the operating system.
        private ProfilerRecorder systemUsedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
        private ProfilerRecorder totalUsedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
        private ProfilerRecorder gcUsedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory"); // Mono/IL2CPP heap size
        private ProfilerRecorder gcAllocatedInFrameRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");

        private ProfilerRecorder mainThreadTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", FRAME_BUFFER_SIZE);
        private ProfilerRecorder gpuFrameTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", FRAME_BUFFER_SIZE);

        public long TotalUsedMemoryInBytes => totalUsedMemoryRecorder.CurrentValue;
        public long SystemUsedMemoryInBytes => systemUsedMemoryRecorder.CurrentValue;
        public long GcUsedMemoryInBytes => gcUsedMemoryRecorder.CurrentValue;
        public float TotalGcAlloc => GetRecorderSamplesSum(gcAllocatedInFrameRecorder);

        public ulong CurrentFrameTimeValueNs => (ulong)mainThreadTimeRecorder.CurrentValue;

        public ulong LastFrameTimeValueNs => (ulong)mainThreadTimeRecorder.LastValue;
        public ulong LastGpuFrameTimeValueNs => (ulong)gpuFrameTimeRecorder.LastValue;

        public void Dispose()
        {
            systemUsedMemoryRecorder.Dispose();
            totalUsedMemoryRecorder.Dispose();
            gcUsedMemoryRecorder.Dispose();
            gcAllocatedInFrameRecorder.Dispose();

            mainThreadTimeRecorder.Dispose();
            gpuFrameTimeRecorder.Dispose();
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
}
