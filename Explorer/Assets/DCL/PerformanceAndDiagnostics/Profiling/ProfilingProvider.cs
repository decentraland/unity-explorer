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

        public ulong TotalUsedMemoryInBytes => (ulong)totalUsedMemoryRecorder.LastValue;

        public ulong CurrentFrameTimeValueInNS => (ulong)mainThreadTimeRecorder.CurrentValue;

        public double AverageFrameTimeValueInNS => GetRecorderAverage(mainThreadTimeRecorder);
        public long MinFrameTimeValueInNS => hiccupBufferCounter.MaxFrameTimeInNS;
        public long MaxFrameTimeValueInNS => hiccupBufferCounter.MinFrameTimeInNS;

        public ulong HiccupCountInBuffer => hiccupBufferCounter.HiccupsCountInBuffer;

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
    }
}
