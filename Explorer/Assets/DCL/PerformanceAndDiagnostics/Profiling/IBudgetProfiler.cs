using System;

namespace DCL.Profiling
{
    public interface IBudgetProfiler : IDisposable
    {
        long TotalUsedMemoryInBytes { get; }
        long SystemUsedMemoryInBytes { get; }

        ulong CurrentFrameTimeValueNs { get; }

        ulong LastFrameTimeValueNs { get; }
        ulong LastGpuFrameTimeValueNs { get; }
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
