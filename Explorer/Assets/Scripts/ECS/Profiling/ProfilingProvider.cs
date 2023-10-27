using Unity.Profiling;

namespace ECS.Profiling
{
    /// <summary>
    ///     Profiling provider to provide in game metrics. Profiler recorder returns values in NS, so to stay consistent with it,
    ///     our most used metric is going to be NS
    /// </summary>
    public class ProfilingProvider : IProfilingProvider
    {
        public const long BYTES_IN_MEGABYTE = 1024 * 1024;
        private const int HICCUP_THRESHOLD_IN_NS = 50_000_000;
        private const int HICCUP_BUFFER_SIZE = 1_000;
        private readonly LinealBufferHiccupCounter hiccupBufferCounter;
        private readonly ProfilerRecorder totalUsedMemoryRecorder;
        private ProfilerRecorder mainThreadTimeRecorder;

        public long TotalUsedMemoryInBytes => totalUsedMemoryRecorder.LastValue;
        public float TotalUsedMemoryInMB => totalUsedMemoryRecorder.LastValue / BYTES_IN_MEGABYTE;

        public long CurrentFrameTimeValueInNS => mainThreadTimeRecorder.CurrentValue;
        public double AverageFrameTimeValueInNS => GetRecorderFPSAverage(mainThreadTimeRecorder);
        public ulong HiccupCountInBuffer => hiccupBufferCounter.HiccupsCountInBuffer;

        public ProfilingProvider()
        {
            totalUsedMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            mainThreadTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            hiccupBufferCounter = new LinealBufferHiccupCounter(HICCUP_BUFFER_SIZE, HICCUP_THRESHOLD_IN_NS);
        }

        public void CheckHiccup() =>
            hiccupBufferCounter.AddDeltaTime(mainThreadTimeRecorder.LastValue);

        private static double GetRecorderFPSAverage(ProfilerRecorder recorder)
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
