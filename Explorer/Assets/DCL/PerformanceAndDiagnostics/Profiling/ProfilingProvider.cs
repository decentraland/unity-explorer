using System;
using Unity.Profiling;

namespace DCL.Profiling
{
    /// <summary>
    ///     Profiling provider to provide in game metrics. Profiler recorder returns values in NS, so to stay consistent with it,
    ///     our most used metric is going to be NS
    /// </summary>
    public class ProfilingProvider : IProfilingProvider
    {
        private const int HICCUP_THRESHOLD_IN_NS = 50_000_000;
        private const int HICCUP_BUFFER_SIZE = 1_000;

        private readonly LinearBufferHiccupCounter hiccupBufferCounter = new (HICCUP_BUFFER_SIZE, HICCUP_THRESHOLD_IN_NS);

        private readonly ProfilerRecorder totalUsedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
        private readonly ProfilerRecorder mainThreadTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
        private readonly ProfilerRecorder gpuRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", 15);

        public ulong TotalUsedMemoryInBytes => (ulong)totalUsedMemoryRecorder.CurrentValue;
        public ulong CurrentFrameTimeValueInNS => (ulong)mainThreadTimeRecorder.CurrentValue;
        public long LastFrameTimeValueInNS => mainThreadTimeRecorder.LastValue;
        public long LastGPUFrameTimeValueInNS => gpuRecorder.LastValue;

        public double AverageFrameTimeInNS => GetRecorderAverage(mainThreadTimeRecorder);

        public float MedianFrameTimeInNS { get; }

        public int AverageFameTimeSamples => mainThreadTimeRecorder.Capacity;

        public long MinFrameTimeInNS => hiccupBufferCounter.MinFrameTimeInNS;
        public long MaxFrameTimeInNS => hiccupBufferCounter.MaxFrameTimeInNS;

        public ulong HiccupCountInBuffer => hiccupBufferCounter.HiccupsCountInBuffer;
        public int HiccupCountBufferSize => hiccupBufferCounter.BufferSize;

        public void Dispose()
        {
            totalUsedMemoryRecorder.Dispose();
            mainThreadTimeRecorder.Dispose();
            gpuRecorder.Dispose();
        }

        public float GetPercentileFrameTime(float percentile) =>
            throw new NotImplementedException();

        public void CheckHiccup() =>
            hiccupBufferCounter.AddDeltaTime(mainThreadTimeRecorder.LastValue);

        private static double GetRecorderAverage(ProfilerRecorder recorder)
        {
            int samplesCount = recorder.Capacity;

            if (samplesCount == 0)
                return 0;

            double r = 0;

            unsafe
            {
                ProfilerRecorderSample* samples = stackalloc ProfilerRecorderSample[samplesCount];
                recorder.CopyTo(samples, samplesCount);

                for (var i = 0; i < samplesCount; ++i)
                    r += samples[i].Value;

                r /= samplesCount;
            }

            return r;
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
}
