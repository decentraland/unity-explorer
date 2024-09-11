using System;
using System.Collections.Generic;
using System.Text;
using Unity.Profiling;

namespace DCL.Profiling
{
    /// <summary>
    ///     Profiling provider to provide in game metrics. Profiler recorder returns values in NS, so to stay consistent with it,
    ///     our most used metric is going to be NS
    /// </summary>
    public class Profiler : IDebugViewProfiler, IAnalyticsReportProfiler
    {
        private const float NS_TO_MS = 1e-6f; // nanoseconds to milliseconds
        private const int HICCUP_THRESHOLD_IN_NS = 50_000_000; // 50 ms ~ 20 FPS
        private const int FRAME_BUFFER_SIZE = 1_000; // 1000 samples: for 30 FPS it's 33 seconds gameplay, for 60 FPS it's 16.6 seconds

        private static readonly ProfilerRecorderSampleComparer PROFILER_SAMPLES_COMPARER = new ();
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

        public ulong CurrentFrameTimeValueNs => (ulong)mainThreadTimeRecorder.CurrentValue;
        public long LastFrameTimeValueNs => mainThreadTimeRecorder.LastValue;
        public float TotalGcAlloc => GetRecorderSamplesSum(gcAllocatedInFrameRecorder);

        private readonly StringBuilder stringBuilder = new ();

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

        public (AnalyticsFrameTimeReport? gpuFrameTime, AnalyticsFrameTimeReport? mainThreadFrameTime, string mainThreadSamples) GetFrameTimesNs(int[] percentile)
        {
            var gpuReport = GetFrameStatsWithPercentiles(gpuFrameTimeRecorder, percentile);
            // Main thread should be last to calculate, so Samples reflects mainThread FrameData and not GPU FrameData
            var mainThreadReport = GetFrameStatsWithPercentiles(mainThreadTimeRecorder, percentile);

            return (gpuReport,mainThreadReport, GetSamplesArrayAsString());

            string GetSamplesArrayAsString()
            {
                stringBuilder.Clear();
                stringBuilder.Append("[");

                for (var i = 0; i < samples.Count; i++)
                {
                    stringBuilder.AppendFormat("{0:0.000}", samples[i].Value * NS_TO_MS);

                    if (i < samples.Count - 1)
                        stringBuilder.Append(",");
                }

                stringBuilder.Append("]");

                return stringBuilder.ToString();
            }
        }

        // Exclusive percentile calculation variant, it rounds to nearest (in contrast to inclusive approach)
        private AnalyticsFrameTimeReport? GetFrameStatsWithPercentiles(ProfilerRecorder recorder, int[] percentile)
        {
            int samplesCount = recorder.Capacity;

            if (samplesCount == 0)
                return null;

            long hiccupCount = 0;
            long hiccupTotalTime = 0;
            long hiccupMin = -1;
            long hiccupMax = -1;

            long sumTime = 0;

            samples.Clear();
            recorder.CopyTo(samples);

            for (var i = 0; i < samples.Count; i++)
            {
                long frameTime = samples[i].Value;

                sumTime += frameTime;

                if (frameTime > HICCUP_THRESHOLD_IN_NS)
                {
                    hiccupCount++;
                    hiccupTotalTime += frameTime;

                    if (frameTime > hiccupMax) hiccupMax = frameTime;

                    if (hiccupMin == -1) hiccupMin = frameTime;
                    else if (frameTime < hiccupMin) hiccupMin = frameTime;
                }
            }

            samples.Sort(PROFILER_SAMPLES_COMPARER);
            var result = new long[percentile.Length];

            for (var i = 0; i < percentile.Length; i++)
            {
                var index = (int)Math.Ceiling(percentile[i] / 100f * samplesCount);
                index = Math.Min(index, samplesCount - 1);
                result[i] = samples[index - 1].Value;
            }

            return
                new AnalyticsFrameTimeReport(
                    new FrameTimeStats(samples[0].Value, samples[samplesCount - 1].Value, hiccupCount),
                    result, sumTime, samplesCount,
                    hiccupCount != 0 ? new HiccupsReport(hiccupTotalTime, hiccupMin, hiccupMax, hiccupTotalTime / hiccupCount) : new HiccupsReport(0, 0, 0, 0)
                );
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

        private class ProfilerRecorderSampleComparer : IComparer<ProfilerRecorderSample>
        {
            public int Compare(ProfilerRecorderSample x, ProfilerRecorderSample y) =>
                x.Value.CompareTo(y.Value);
        }
    }
}
