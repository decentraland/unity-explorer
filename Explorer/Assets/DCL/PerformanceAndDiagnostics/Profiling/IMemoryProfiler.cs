namespace DCL.Profiling
{
    public interface IMemoryProfiler : IBudgetProfiler
    {
        long SystemUsedMemoryInBytes { get; }
        long GcUsedMemoryInBytes { get; }

        /// <summary>
        ///     Memory consumed by the Unity Profiler itself, already excluded from
        ///     <see cref="IBudgetProfiler.TotalUsedMemoryInBytes" />. Zero in release builds.
        /// </summary>
        long ProfilerUsedMemoryInBytes { get; }

        /// <summary>
        ///     Memory reserved by the Unity Profiler (>= used), already excluded from
        ///     <see cref="SystemUsedMemoryInBytes" /> since reserved pools count toward the
        ///     OS-level footprint. Zero in release builds.
        /// </summary>
        long ProfilerReservedMemoryInBytes { get; }

        float TotalGcAlloc { get; }
    }
}
