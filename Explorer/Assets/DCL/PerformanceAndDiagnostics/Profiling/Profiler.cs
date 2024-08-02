using System;
using Unity.Profiling;

namespace DCL.Profiling
{
    /// <summary>
    ///     Profiling provider to provide in game metrics. Profiler recorder returns values in NS, so to stay consistent with it,
    ///     our most used metric is going to be NS
    /// </summary>
    public class Profiler : IDebugViewProfiler, IAnalyticsReportProfiler
    {
        private const int HICCUP_THRESHOLD_IN_NS = 50_000_000; // 50 ms ~ 20 FPS
        private const int FRAME_BUFFER_SIZE = 1_000; // 1000 samples: for 30 FPS it's 33 seconds gameplay, for 60 FPS it's 16.6 seconds

        private readonly long[] samplesArray = new long[FRAME_BUFFER_SIZE];

        // Memory footprint of your application as seen by the operating system.
        private ProfilerRecorder systemUsedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
        private ProfilerRecorder totalUsedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
        private ProfilerRecorder gcUsedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Used Memory"); // Mono/IL2CPP heap size

        private ProfilerRecorder mainThreadTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", FRAME_BUFFER_SIZE);
        private ProfilerRecorder gpuFrameTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", FRAME_BUFFER_SIZE);

        public long SystemUsedMemoryInBytes => systemUsedMemoryRecorder.CurrentValue;
        public long TotalUsedMemoryInBytes => totalUsedMemoryRecorder.CurrentValue;
        public long GcUsedMemoryInBytes => gcUsedMemoryRecorder.CurrentValue;

        public ulong CurrentFrameTimeValueNs => (ulong)mainThreadTimeRecorder.CurrentValue;
        public long LastFrameTimeValueNs => mainThreadTimeRecorder.LastValue;

        public void Dispose()
        {
            systemUsedMemoryRecorder.Dispose();
            mainThreadTimeRecorder.Dispose();
            gpuFrameTimeRecorder.Dispose();
        }

        /// <summary>
        ///     In nanoseconds
        /// </summary>
        public FrameTimeStats? CalculateMainThreadFrameTimesNs() =>
            CalculateFrameTimeStats(mainThreadTimeRecorder);

        private static FrameTimeStats? CalculateFrameTimeStats(ProfilerRecorder recorder)
        {
            int availableSamples = recorder.Capacity;

            if (availableSamples == 0)
                return null;

            long minFrameTime = long.MaxValue;
            long maxFrameTime = long.MinValue;
            long hiccupCount = 0;

            unsafe
            {
                ProfilerRecorderSample* samples = stackalloc ProfilerRecorderSample[availableSamples];
                recorder.CopyTo(samples, availableSamples);

                for (var i = 0; i < availableSamples; ++i)
                {
                    long frameTime = samples[i].Value;

                    if (frameTime > HICCUP_THRESHOLD_IN_NS) hiccupCount++;
                    if (frameTime < minFrameTime) minFrameTime = frameTime;
                    if (frameTime > maxFrameTime) maxFrameTime = frameTime;
                }
            }

            return new FrameTimeStats(minFrameTime, maxFrameTime, hiccupCount);
        }

        public AnalyticsFrameTimeReport? GetMainThreadFramesNs(int[] percentile) =>
            GetFrameStatsWithPercentiles(mainThreadTimeRecorder, percentile);

        public AnalyticsFrameTimeReport? GetGpuThreadFramesNs(int[] percentile) =>
            GetFrameStatsWithPercentiles(gpuFrameTimeRecorder, percentile);

        // Exclusive percentile calculation variant, it rounds to nearest (in contrast to inclusive approach)
        private AnalyticsFrameTimeReport? GetFrameStatsWithPercentiles(ProfilerRecorder recorder, int[] percentile)
        {
            int samplesCount = recorder.Capacity;

            if (samplesCount == 0)
                return null;

            Array.Clear(samplesArray, 0, samplesArray.Length);
            long hiccupCount = 0;
            long sumTime = 0;

            unsafe
            {
                ProfilerRecorderSample* samples = stackalloc ProfilerRecorderSample[samplesCount];
                recorder.CopyTo(samples, samplesCount);

                for (var i = 0; i < samplesCount; ++i)
                {
                    long frameTime = samples[i].Value;

                    samplesArray[i] = frameTime;
                    sumTime += frameTime;

                    if (frameTime > HICCUP_THRESHOLD_IN_NS)
                        hiccupCount++;
                }
            }

            Array.Sort(samplesArray);

            var result = new long[percentile.Length];

            for (var i = 0; i < percentile.Length; i++)
            {
                var index = (int)Math.Ceiling(percentile[i] / 100f * samplesCount);
                index = Math.Min(index, samplesCount - 1);
                result[i] = samplesArray[index - 1];
            }

            return
                new AnalyticsFrameTimeReport(
                    new FrameTimeStats(samplesArray[0], samplesArray[samplesCount - 1], hiccupCount),
                    result, sumTime, samplesCount);
        }
    }
}
