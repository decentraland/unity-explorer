using DCL.PerformanceAndDiagnostics.Profiling;
using System.Collections.Generic;
using UnityEngine;
using static DCL.Optimization.PerformanceBudgeting.MemoryUsageStatus;

namespace DCL.Optimization.PerformanceBudgeting
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

        private readonly IProfilingProvider profilingProvider;
        private readonly Dictionary<MemoryUsageStatus, float> memoryThreshold;
        private readonly ISystemMemory systemMemory;

        // Debug
        private readonly bool isReleaseBuild = !Debug.isDebugBuild;
        public MemoryUsageStatus SimulatedMemoryUsage { private get; set; }

        private ulong actualSystemMemory => systemMemory.TotalSizeInMB;

        public MemoryBudgetProvider(IProfilingProvider profilingProvider, Dictionary<MemoryUsageStatus, float> memoryThreshold)
        {
            SimulatedMemoryUsage = Normal;
            systemMemory = new StandaloneSystemMemory();

            this.profilingProvider = profilingProvider;
            this.memoryThreshold = memoryThreshold;
        }

        public MemoryUsageStatus GetMemoryUsageStatus()
        {
            ulong usedMemory = profilingProvider.TotalUsedMemoryInBytes / BYTES_IN_MEGABYTE;
            ulong totalSystemMemory = GetTotalSystemMemory();

            return usedMemory switch
                   {
                       _ when usedMemory > totalSystemMemory * memoryThreshold[Full] => Full,
                       _ when usedMemory > totalSystemMemory * memoryThreshold[Warning] => Warning,
                       _ => Normal,
                   };
        }

        public (float warning, float full) GetMemoryRanges()
        {
            ulong totalSizeInMB = GetTotalSystemMemory();
            return (totalSizeInMB * memoryThreshold[Warning], totalSizeInMB * memoryThreshold[Full]);
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
                (ulong)(profilingProvider.TotalUsedMemoryInBytes / BYTES_IN_MEGABYTE / (memoryThreshold[Warning] * 1.1f));
        }
    }
}
