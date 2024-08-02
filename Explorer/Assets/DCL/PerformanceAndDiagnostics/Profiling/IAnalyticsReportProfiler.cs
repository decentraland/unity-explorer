namespace DCL.Profiling
{
    public interface IAnalyticsReportProfiler : IBudgetProfiler
    {
        long TotalUsedMemoryInBytes { get; }

        long GcUsedMemoryInBytes { get; }

        AnalyticsFrameTimeReport? GetMainThreadFramesNs(int[] percentile);

        AnalyticsFrameTimeReport? GetGpuThreadFramesNs(int[] percentile);
    }

    public readonly struct AnalyticsFrameTimeReport
    {
        public readonly int Samples;
        public readonly long SumTime;
        public readonly long Average;
        public readonly long[] Percentiles;
        public readonly FrameTimeStats Stats;

        public AnalyticsFrameTimeReport(FrameTimeStats stats, long[] percentiles, long sumTime, int samples)
        {
            Samples = samples;
            SumTime = sumTime;

            Average = sumTime / samples;
            Percentiles = percentiles;
            Stats = stats;
        }
    }
}
