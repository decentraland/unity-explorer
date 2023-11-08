#if UNITY_EDITOR
using DCL.PerformanceAndDiagnostics.PerformanceBudgeting.Memory.Editor;
#endif
using DCL.Profiling;
using static DCL.PerformanceBudgeting.MemoryUsageStatus;
using static DCL.PerformanceBudgeting.BudgetingConfig;

namespace DCL.PerformanceBudgeting
{
    public enum MemoryUsageStatus
    {
        Normal,
        Warning,
        Full,
    }

    public class MemoryBudgetProvider : IConcurrentBudgetProvider
    {
        private readonly IProfilingProvider profilingProvider;
        private readonly ISystemMemory systemMemory;

        public MemoryBudgetProvider(IProfilingProvider profilingProvider)
        {
            systemMemory = new StandaloneSystemMemory();

            this.profilingProvider = profilingProvider;
        }

        public MemoryUsageStatus GetMemoryUsageStatus()
        {
            long usedMemory = profilingProvider.TotalUsedMemoryInBytes / ProfilingProvider.BYTES_IN_MEGABYTE;

            return usedMemory switch
                   {
                       _ when usedMemory > GetTotalSystemMemory() * MEM_THRESHOLD[Full] => Full,
                       _ when usedMemory > GetTotalSystemMemory() * MEM_THRESHOLD[Warning] => Warning,
                       _ => Normal,
                   };
        }

        public (float warning, float full) GetMemoryRanges()
        {
            long totalSizeInMB = GetTotalSystemMemory();
            return (totalSizeInMB * MEM_THRESHOLD[Warning], totalSizeInMB * MEM_THRESHOLD[Full]);
        }

        public bool TrySpendBudget() =>
            GetMemoryUsageStatus() != Full;

        public void ReleaseBudget() { }

        private long GetTotalSystemMemory()
        {
#if UNITY_EDITOR
            if (MemoryBudgetDebug.FlagFull)
                return 0;

            if (MemoryBudgetDebug.FlagWarning)
                return (long)(profilingProvider.TotalUsedMemoryInBytes / ProfilingProvider.BYTES_IN_MEGABYTE / (MEM_THRESHOLD[Warning] * 1.1f));
#endif

            return systemMemory.TotalSizeInMB;
        }
    }
}
