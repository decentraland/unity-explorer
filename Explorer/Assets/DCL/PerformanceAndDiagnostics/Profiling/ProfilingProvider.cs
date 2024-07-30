using System;
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

        private ProfilerRecorder totalUsedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
        private ProfilerRecorder mainThreadTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", FRAME_BUFFER_SIZE);

        private ProfilerRecorder gpuRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", FRAME_BUFFER_SIZE);

        public long TotalUsedMemoryInBytes => totalUsedMemoryRecorder.CurrentValue;
        public ulong CurrentFrameTimeValueNs => (ulong)mainThreadTimeRecorder.CurrentValue;
        public long LastFrameTimeValueNs => mainThreadTimeRecorder.LastValue;

        public FrameTimeStats? FrameTimeStatsNs => CalculateFrameStatsData(mainThreadTimeRecorder); // in NS (nanoseconds)

        // public long LastFrameTimeValueInNS => mainThreadTimeRecorder.LastValue;
        // public long LastGPUFrameTimeValueInNS => gpuRecorder.LastValue;
        // public float MedianFrameTimeInNS { get; }
        // public int AverageFameTimeSamples => mainThreadTimeRecorder.Capacity;
        // public int HiccupCountBufferSize => hiccupBufferCounter.BufferSize;

        public void Dispose()
        {
            totalUsedMemoryRecorder.Dispose();
            mainThreadTimeRecorder.Dispose();
            gpuRecorder.Dispose();
        }

        private static FrameTimeStats? CalculateFrameStatsData(ProfilerRecorder recorder, int longSamplesAmount = FRAME_BUFFER_SIZE)
        {
            int availableSamples = recorder.Capacity;

            if (availableSamples == 0 || longSamplesAmount == 0)
                return null;

            // Ensure we do not exceed recorder capacity
            longSamplesAmount = Math.Min(longSamplesAmount, availableSamples);

            long minFrameTime = long.MaxValue;
            long maxFrameTime = long.MinValue;
            long hiccupCount = 0;

            unsafe
            {
                ProfilerRecorderSample* samples = stackalloc ProfilerRecorderSample[longSamplesAmount];
                recorder.CopyTo(samples, longSamplesAmount);

                for (var i = 0; i < longSamplesAmount; ++i)
                {
                    long frameTime = samples[i].Value;

                    if (frameTime > HICCUP_THRESHOLD_IN_NS) hiccupCount++;
                    if (frameTime < minFrameTime) minFrameTime = frameTime;
                    if (frameTime > maxFrameTime) maxFrameTime = frameTime;
                }
            }

            return new FrameTimeStats(minFrameTime, maxFrameTime, hiccupCount);
        }

        public double[] GetFrameTimePercentiles(int[] percentile) =>
            GetPercentiles(mainThreadTimeRecorder, percentile);

        private static double[] GetPercentiles(ProfilerRecorder recorder, int[] percentile)
        {
            int samplesCount = recorder.Capacity;

            if (samplesCount == 0 || percentile.Length == 0)
                return default(double[]);

            var samplesArray = new long[samplesCount];

            unsafe
            {
                ProfilerRecorderSample* samples = stackalloc ProfilerRecorderSample[samplesCount];
                recorder.CopyTo(samples, samplesCount);

                for (var i = 0; i < samplesCount; ++i)
                    samplesArray[i] = samples[i].Value;
            }

            Array.Sort(samplesArray);

            var result = new double[percentile.Length];

            for (var i = 0; i < percentile.Length; i++)
            {
                var k = (int)Math.Ceiling(percentile[i] / 100.0 * samplesCount);
                result[i] = samplesArray[k - 1];
            }

            return result;
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
