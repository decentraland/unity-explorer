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
        private const ulong BYTES_IN_MEGABYTE = 1024 * 1024;
        private const ulong NO_MEMORY = 0;

        public static MemoryUsageStatus SimulatedMemoryUsage;

        private readonly IProfilingProvider profilingProvider;
        private readonly ISystemMemory systemMemory;

        private readonly bool isReleaseBuild = !Debug.isDebugBuild;

        private ulong actualSystemMemory => systemMemory.TotalSizeInMB;

        public MemoryBudgetProvider(IProfilingProvider profilingProvider)
        {
            SimulatedMemoryUsage = Normal;
            systemMemory = new StandaloneSystemMemory();

            this.profilingProvider = profilingProvider;
        }

        public MemoryUsageStatus GetMemoryUsageStatus()
        {
            ulong usedMemory = profilingProvider.TotalUsedMemoryInBytes / BYTES_IN_MEGABYTE;
            ulong totalSystemMemory = GetTotalSystemMemory();

            return usedMemory switch
                   {
                       _ when usedMemory > totalSystemMemory * MEM_THRESHOLD[Full] => Full,
                       _ when usedMemory > totalSystemMemory * MEM_THRESHOLD[Warning] => Warning,
                       _ => Normal,
                   };
        }

        public (float warning, float full) GetMemoryRanges()
        {
            ulong totalSizeInMB = GetTotalSystemMemory();
            return (totalSizeInMB * MEM_THRESHOLD[Warning], totalSizeInMB * MEM_THRESHOLD[Full]);
        }

        public bool TrySpendBudget() =>
            GetMemoryUsageStatus() != Full;

        public void ReleaseBudget() { }

        private ulong GetTotalSystemMemory() =>
            isReleaseBuild ? actualSystemMemory : GetSimulatedSystemMemory();

        private ulong GetSimulatedSystemMemory()
        {
            return SimulatedMemoryUsage switch
                   {
                       Full => NO_MEMORY,
                       Warning => CalculateSystemMemoryForWarningThreshold(),
                       _ => actualSystemMemory,
                   };

            // ReSharper disable once PossibleLossOfFraction
            ulong CalculateSystemMemoryForWarningThreshold() => // 10% higher than Warning threshold for current usedMemory
                (ulong)(profilingProvider.TotalUsedMemoryInBytes / BYTES_IN_MEGABYTE / (MEM_THRESHOLD[Warning] * 1.1f));
        }
    }
}
