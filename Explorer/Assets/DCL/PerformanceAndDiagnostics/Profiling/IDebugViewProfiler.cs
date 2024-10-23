namespace DCL.Profiling
{
    public interface IDebugViewProfiler : IMemoryProfiler
    {
        FrameTimeStats? CalculateMainThreadFrameTimesNs();
    }
}
