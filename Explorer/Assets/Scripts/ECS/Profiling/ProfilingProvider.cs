using Cysharp.Threading.Tasks;
using Unity.Profiling;

namespace ECS.Profiling
{
    /// <summary>
    /// Profiling provider to provide in game metrics. Profiler recorder returns values in NS, so to stay consistent with it,
    /// our most used metric is going to be NS
    /// </summary>
    public class ProfilingProvider  : IProfilingProvider
    {
        private ProfilerRecorder mainThreadTimeRecorder;
        private const int HICCUP_THRESHOLD_IN_NS = 50_000_000;
        private const int HICCUP_BUFFER_SIZE = 1_000;
        private LinealBufferHiccupCounter hiccupBufferCounter;

        public ProfilingProvider()
        {
            mainThreadTimeRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 15);
            hiccupBufferCounter = new LinealBufferHiccupCounter(HICCUP_BUFFER_SIZE, HICCUP_THRESHOLD_IN_NS);
            CountHiccup().Forget();
        }

        public long GetCurrentFrameTimeValueInNS() =>
            mainThreadTimeRecorder.CurrentValue;

        public double GetAverageFrameTimeValueInNS() =>
            GetRecorderFPSAverage(mainThreadTimeRecorder);


        public int GetHiccupCountInBuffer() =>
            hiccupBufferCounter.HiccupsCountInBuffer;

        private async UniTaskVoid CountHiccup()
        {
            while (true)
            {
                await UniTask.Yield();
                hiccupBufferCounter.AddDeltaTime(mainThreadTimeRecorder.LastValue);
            }
        }

        private double GetRecorderFPSAverage(ProfilerRecorder recorder)
        {
            var samplesCount = recorder.Capacity;
            if (samplesCount == 0)
                return 0;

            double r = 0;
            unsafe
            {
                var samples = stackalloc ProfilerRecorderSample[samplesCount];
                recorder.CopyTo(samples, samplesCount);
                for (var i = 0; i < samplesCount; ++i)
                    r += samples[i].Value;
                r /= samplesCount;
            }

            return r;
        }

    }

}


