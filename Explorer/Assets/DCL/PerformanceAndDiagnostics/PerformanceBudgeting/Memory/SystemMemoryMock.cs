using DCL.Profiling;
#if UNITY_EDITOR
using DCL.PerformanceAndDiagnostics.PerformanceBudgeting.Memory.Editor;
#endif

namespace DCL.PerformanceBudgeting.Memory
{
    public class SystemMemoryMock : ISystemMemory
    {
        private readonly IProfilingProvider profilingProvider;
        private readonly long totalSizeInMB;

        public SystemMemoryMock(IProfilingProvider profilingProvider, long totalSizeInMB)
        {
            this.profilingProvider = profilingProvider;
            this.totalSizeInMB = totalSizeInMB;
        }

        public long GetTotalSizeInMB()
        {
#if UNITY_EDITOR
            if (MemoryBudgetDebug.flagFull)
                return 0;

            if (MemoryBudgetDebug.flagWarning)
                return (long)(profilingProvider.TotalUsedMemoryInBytes / ProfilingProvider.BYTES_IN_MEGABYTE / 0.6);
#endif

            return totalSizeInMB;
        }
    }
}
