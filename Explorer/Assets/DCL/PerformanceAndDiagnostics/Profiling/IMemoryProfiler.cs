namespace DCL.Profiling
{
    public interface IMemoryProfiler : IBudgetProfiler
    {
        long SystemUsedMemoryInBytes { get; }
        long GcUsedMemoryInBytes { get; }

        /// <summary>
        ///     Memory consumed by the Unity Profiler itself, already excluded from
        ///     <see cref="SystemUsedMemoryInBytes" /> and <see cref="IBudgetProfiler.TotalUsedMemoryInBytes" />.
        ///     Zero in release builds.
        /// </summary>
        long ProfilerUsedMemoryInBytes { get; }

        float TotalGcAlloc { get; }
    }
}
