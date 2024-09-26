using System;

namespace DCL.Profiling
{
    public interface IBudgetProfiler : IDisposable
    {
        long TotalUsedMemoryInBytes { get; }
        ulong CurrentFrameTimeValueNs { get; }
        long SystemUsedMemoryInBytes { get; }
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
