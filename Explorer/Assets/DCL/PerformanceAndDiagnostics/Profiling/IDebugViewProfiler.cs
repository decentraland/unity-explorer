namespace DCL.Profiling
{
    public interface IDebugViewProfiler : IBudgetProfiler
    {
        long LastFrameTimeValueNs { get; }
        FrameTimeStats? CalculateMainThreadFrameTimesNs();
    }
}
