namespace DCL.Profiling
{
    public interface IDebugViewProfiler : IMemoryProfiler
    {
        long LastFrameTimeValueNs { get; }
        FrameTimeStats? CalculateMainThreadFrameTimesNs();
    }
}
