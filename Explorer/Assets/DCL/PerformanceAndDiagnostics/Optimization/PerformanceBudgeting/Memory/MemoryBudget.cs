#nullable enable

using DCL.Profiling;
using System.Collections.Generic;
using UnityEngine;
using static DCL.Optimization.PerformanceBudgeting.MemoryUsageStatus;

namespace DCL.Optimization.PerformanceBudgeting
{
    public enum MemoryUsageStatus
    {
        NORMAL,
        WARNING,
        FULL,
    }

    public class MemoryBudget : IMemoryUsageProvider, IPerformanceBudget
    {
        private const long BYTES_IN_MEGABYTE = 1024 * 1024;
        private const long NO_MEMORY = 0;

        private readonly IBudgetProfiler profiler;
        private readonly IReadOnlyDictionary<MemoryUsageStatus, float> memoryThreshold;

        public MemoryUsageStatus SimulatedMemoryUsage { private get; set; }

        internal long actualSystemMemory;

        public MemoryBudget(ISystemMemory systemMemory, IBudgetProfiler profiler, IReadOnlyDictionary<MemoryUsageStatus, float> memoryThreshold)
        {
            SimulatedMemoryUsage = NORMAL;
            this.profiler = profiler;
            this.memoryThreshold = memoryThreshold;
            actualSystemMemory = systemMemory.TotalSizeInMB;
        }

        public MemoryUsageStatus GetMemoryUsageStatus()
        {
            var usedMemory = profiler.SystemUsedMemoryInBytes / BYTES_IN_MEGABYTE;
            long totalSystemMemory = GetTotalSystemMemory();

            return usedMemory switch
                   {
                       _ when usedMemory > totalSystemMemory * memoryThreshold[FULL] => FULL,
                       _ when usedMemory > totalSystemMemory * memoryThreshold[WARNING] => WARNING,
                       _ => NORMAL,
                   };
        }

        public (float warning, float full) GetMemoryRanges()
        {
            long totalSizeInMB = GetTotalSystemMemory();
            return (totalSizeInMB * memoryThreshold[WARNING], totalSizeInMB * memoryThreshold[FULL]);
        }

        public bool TrySpendBudget() =>
            GetMemoryUsageStatus() != FULL;

        private long GetTotalSystemMemory()
        {
            return SimulatedMemoryUsage switch
                   {
                       FULL => NO_MEMORY,
                       WARNING => CalculateSystemMemoryForWarningThreshold(),
                       _ => actualSystemMemory,
                   };

            // ReSharper disable once PossibleLossOfFraction
            long CalculateSystemMemoryForWarningThreshold() => // 10% higher than Warning threshold for current usedMemory
                (long)(profiler.SystemUsedMemoryInBytes / BYTES_IN_MEGABYTE / (memoryThreshold[WARNING] * 1.1f));
        }

        public class Default : IPerformanceBudget
        {
            private static readonly IReadOnlyDictionary<MemoryUsageStatus, float> MEMORY_THRESHOLD = new Dictionary<MemoryUsageStatus, float>
            {
                { WARNING, 0.65f },
                { FULL, 0.75f }
            };

            private readonly IPerformanceBudget performanceBudget = new MemoryBudget(
                new StandaloneSystemMemory(),
                new Profiler(),
                MEMORY_THRESHOLD
            );

            public bool TrySpendBudget() =>
                performanceBudget.TrySpendBudget();
        }
    }
}
