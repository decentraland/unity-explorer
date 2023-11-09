using DCL.Profiling;
using UnityEngine;
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
        public static MemoryUsageStatus DebugMode;

        private readonly IProfilingProvider profilingProvider;
        private readonly ISystemMemory systemMemory;

        public MemoryBudgetProvider(IProfilingProvider profilingProvider)
        {
            DebugMode = Normal;
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
            if (!Debug.isDebugBuild) return systemMemory.TotalSizeInMB;

            return DebugMode switch
                   {
                       Full => 0,
                       Warning => (long)(profilingProvider.TotalUsedMemoryInBytes / ProfilingProvider.BYTES_IN_MEGABYTE / (MEM_THRESHOLD[Warning] * 1.1f)),
                       _ => systemMemory.TotalSizeInMB,
                   };
        }
    }
}
