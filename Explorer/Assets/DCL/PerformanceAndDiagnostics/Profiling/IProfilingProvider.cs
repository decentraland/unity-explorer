using System;

namespace DCL.Profiling
{
    public interface IAnalyticsProfiling
    {
        float MedianFrameTimeInNS { get; }
        float GetPercentileFrameTime(float percentile);
    }

    public interface IProfilingProvider : IDisposable
    {
        double[] GetFrameTimePercentiles(int[] percentile);

        ulong TotalUsedMemoryInBytes { get; }

        // Hiccups
        ulong HiccupCountInBuffer { get; }
        int HiccupCountBufferSize { get; }
        void CheckHiccup();

        // GPU
        long LastGPUFrameTimeValueInNS { get; }

        // Total Frame Time ("Main Thread")
        int AverageFameTimeSamples { get; }

        ulong CurrentFrameTimeValueInNS { get; }
        long LastFrameTimeValueInNS { get; }
        long MinFrameTimeInNS { get; }
        long MaxFrameTimeInNS { get; }
        double AverageFrameTimeInNS { get; }

    }
}
