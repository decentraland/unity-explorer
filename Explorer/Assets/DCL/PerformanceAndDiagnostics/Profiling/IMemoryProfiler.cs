namespace DCL.Profiling
{
    public interface IMemoryProfiler : IBudgetProfiler
    {
        long SystemUsedMemoryInBytes { get; }
        long GcUsedMemoryInBytes { get; }
    }
}
