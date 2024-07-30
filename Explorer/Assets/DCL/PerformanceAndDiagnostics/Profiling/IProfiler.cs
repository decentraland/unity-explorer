using System;

namespace DCL.Profiling
{
    public interface IBudgetProfiler : IDisposable
    {
        long TotalUsedMemoryInBytes { get; }
        ulong CurrentFrameTimeValueNs { get; }
    }

    public interface IDebugViewProfiler : IBudgetProfiler
    {
        long LastFrameTimeValueNs { get; }

        FrameTimeStats? CalculateMainThreadFrameTimesNs();
    }

    public interface IAnalyticsReportProfiler : IBudgetProfiler
    {
        AnalyticsFrameTimeReport? GetMainThreadFramesNs(int[] percentile);
    }

    public interface IProfiler : IDebugViewProfiler, IAnalyticsReportProfiler { }
}
