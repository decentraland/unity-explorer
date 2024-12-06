namespace DCL.Profiling
{
    public interface IProfiler : IMemoryProfiler
    {
        FrameTimeStats? CalculateMainThreadFrameTimesNs();

        (bool hasValue, long count, long sumTime, long min, long max, float avg) CalculateMainThreadHiccups();

        (bool hasValue, long count, long sumTime, long min, long max, float avg) CalculateGpuHiccups();
    }
}
