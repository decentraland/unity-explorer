namespace DCL.Profiling
{
    public interface IMemoryProfiler : IBudgetProfiler
    {
        long GcUsedMemoryInBytes { get; }
        float TotalGcAlloc { get; }
    }
}
