namespace DCL.Profiling
{
    public interface IMemoryProfiler : IBudgetProfiler
    {
        long SystemUsedMemoryInBytes { get; }
        long GcUsedMemoryInBytes { get; }

        /// <summary>Profiler used memory, excluded from <see cref="IBudgetProfiler.TotalUsedMemoryInBytes" />. Zero in release builds.</summary>
        long ProfilerUsedMemoryInBytes { get; }

        /// <summary>Profiler reserved memory (>= used), excluded from <see cref="SystemUsedMemoryInBytes" />. Zero in release builds.</summary>
        long ProfilerReservedMemoryInBytes { get; }

        float TotalGcAlloc { get; }
    }
}
