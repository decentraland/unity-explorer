using System;
using Unity.Profiling;
using UnityEngine;

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

        private readonly long[] samplesArray = new long[FRAME_BUFFER_SIZE];

        private ProfilerRecorder totalUsedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
        private ProfilerRecorder mainThreadTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", FRAME_BUFFER_SIZE);

        private ProfilerRecorder gpuRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", FRAME_BUFFER_SIZE);

        public long TotalUsedMemoryInBytes => totalUsedMemoryRecorder.CurrentValue;
        public ulong CurrentFrameTimeValueNs => (ulong)mainThreadTimeRecorder.CurrentValue;
        public long LastFrameTimeValueNs => mainThreadTimeRecorder.LastValue;

        /// <summary>
        /// In nanoseconds
        /// </summary>
        public FrameTimeStats? CalculateMainThreadFrameTimesNs() =>
            CalculateFrameStatistics(mainThreadTimeRecorder);

        public void Dispose()
        {
            totalUsedMemoryRecorder.Dispose();
            mainThreadTimeRecorder.Dispose();
            gpuRecorder.Dispose();
        }

        private static FrameTimeStats? CalculateFrameStatistics(ProfilerRecorder recorder)
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

        private AnalyticsFrameTimeReport? GetFrameStatsWithPercentiles(ProfilerRecorder recorder, int[] percentile)
        {
            int samplesCount = recorder.Capacity;

            if (samplesCount == 0)
                return null;

            long hiccupCount = 0;
            long avgFrameTime = 0;

            unsafe
            {
                ProfilerRecorderSample* samples = stackalloc ProfilerRecorderSample[samplesCount];
                recorder.CopyTo(samples, samplesCount);

                for (var i = 0; i < samplesCount; ++i)
                {
                    long frameTime = samples[i].Value;

                    samplesArray[i] = frameTime;
                    avgFrameTime += frameTime;

                    if (frameTime > HICCUP_THRESHOLD_IN_NS)
                        hiccupCount++;
                }

                avgFrameTime /= samplesCount;
            }

            Array.Sort(samplesArray);

            var result = new long[percentile.Length];
            for (var i = 0; i < percentile.Length; i++)
            {
                var index = (int)Math.Ceiling(percentile[i] / 100f * samplesCount);
                index = Math.Min(index, samplesCount - 1);
                Debug.Log($"VVV {index}");
                result[i] = samplesArray[index - 1];
            }

            return new AnalyticsFrameTimeReport(
                new FrameTimeStats(samplesArray[0], samplesArray[samplesCount - 1], hiccupCount),
                avgFrameTime, result, samplesCount);
        }
    }

    public readonly struct AnalyticsFrameTimeReport
    {
        public readonly int Samples;
        public readonly long Average;
        public readonly long[] Percentiles;
        public readonly FrameTimeStats Stats;

        public AnalyticsFrameTimeReport(FrameTimeStats stats, long average, long[] percentiles, int samples)
        {
            Average = average;
            Percentiles = percentiles;
            Stats = stats;
            Samples = samples;
        }
    }

    public readonly struct FrameTimeStats
    {
        public readonly long MinFrameTime;
        public readonly long MaxFrameTime;
        public readonly long HiccupCount;

        public FrameTimeStats(long minFrameTime, long maxFrameTime, long hiccupCount)
        {
            MinFrameTime = minFrameTime;
            MaxFrameTime = maxFrameTime;
            HiccupCount = hiccupCount;
        }
    }
}
