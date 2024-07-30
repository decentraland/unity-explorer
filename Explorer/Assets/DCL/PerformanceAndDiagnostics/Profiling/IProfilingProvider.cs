using System;

namespace DCL.Profiling
{
    public interface IDebugViewProfiler : IBudgetProfiler
    {
        FrameTimeStats? FrameTimeStatsNs { get; }
        public long LastFrameTimeValueNs { get; }
    }

    public interface IBudgetProfiler
    {
        public long TotalUsedMemoryInBytes { get; }
        public ulong CurrentFrameTimeValueNs { get; }
    }

    public interface IProfiler : IDebugViewProfiler, IDisposable
    {
    }
}
