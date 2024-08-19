namespace DCL.Profiling
{
    public interface IDebugViewProfiler : IMemoryProfiler
    {
        long LastFrameTimeValueNs { get; }
        FrameTimeStats? CalculateMainThreadFrameTimesNs();
    }

    public interface IMemoryProfiler : IBudgetProfiler
    {
        long SystemUsedMemoryInBytes { get; }
        long GcUsedMemoryInBytes { get; }
    }
}
