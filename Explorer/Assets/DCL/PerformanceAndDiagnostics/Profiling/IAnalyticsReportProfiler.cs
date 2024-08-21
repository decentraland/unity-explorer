namespace DCL.Profiling
{
    public interface IAnalyticsReportProfiler : IMemoryProfiler
    {
        (AnalyticsFrameTimeReport?, long[]) GetMainThreadFramesNs(int[] percentile);

        AnalyticsFrameTimeReport? GetGpuThreadFramesNs(int[] percentile);
    }

    public readonly struct AnalyticsFrameTimeReport
    {
        public readonly int Samples;
        public readonly long SumTime;
        public readonly long Average;
        public readonly long[] Percentiles;
        public readonly FrameTimeStats Stats;
        public readonly HiccupsReport HiccupsReport;

        public AnalyticsFrameTimeReport(FrameTimeStats stats, long[] percentiles, long sumTime, int samples, HiccupsReport hiccupsReport)
        {
            Samples = samples;
            SumTime = sumTime;

            Average = sumTime / samples;
            Percentiles = percentiles;
            Stats = stats;

            HiccupsReport = hiccupsReport;
        }
    }

    public readonly struct HiccupsReport
    {
        public readonly long HiccupsTime;
        public readonly long HiccupsMin;
        public readonly long HiccupsMax;
        public readonly long HiccupsAvg;

        public HiccupsReport(long hiccupsTime, long hiccupsMin, long hiccupsMax, long hiccupsAvg)
        {
            HiccupsTime = hiccupsTime;
            HiccupsMin = hiccupsMin;
            HiccupsMax = hiccupsMax;
            HiccupsAvg = hiccupsAvg;
        }
    }
}
