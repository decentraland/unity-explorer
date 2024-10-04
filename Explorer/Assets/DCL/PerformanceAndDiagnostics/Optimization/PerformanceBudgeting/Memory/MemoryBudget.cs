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

        private readonly ISystemMemoryCap systemMemoryCap;
        private readonly IBudgetProfiler profiler;
        private readonly IReadOnlyDictionary<MemoryUsageStatus, float> memoryThreshold;

        public MemoryUsageStatus SimulatedMemoryUsage { private get; set; }

        public MemoryBudget(ISystemMemoryCap systemMemoryCap, IBudgetProfiler profiler, IReadOnlyDictionary<MemoryUsageStatus, float> memoryThreshold)
        {
            SimulatedMemoryUsage = NORMAL;

            this.systemMemoryCap = systemMemoryCap;
            this.profiler = profiler;
            this.memoryThreshold = memoryThreshold;
        }

        public MemoryUsageStatus GetMemoryUsageStatus()
        {
            long usedMemory = profiler.SystemUsedMemoryInBytes / BYTES_IN_MEGABYTE;
            long totalSystemMemory = GetTotalSystemMemory();

            return usedMemory switch
                   {
                       _ when usedMemory > totalSystemMemory * memoryThreshold[FULL] => FULL,
                       _ when usedMemory > totalSystemMemory * memoryThreshold[WARNING] => WARNING,
                       _ => NORMAL,
                   };
        }

        public (int warning, int full) GetMemoryRanges()
        {
            long totalSizeInMB = GetTotalSystemMemory();
            return ((int) (totalSizeInMB * memoryThreshold[WARNING]), (int)(totalSizeInMB * memoryThreshold[FULL]));
        }

        public bool TrySpendBudget() =>
            GetMemoryUsageStatus() != FULL;

        private long GetTotalSystemMemory()
        {
            return SimulatedMemoryUsage switch
                   {
                       FULL => NO_MEMORY,
                       WARNING => CalculateSystemMemoryForWarningThreshold(),
                       _ => systemMemoryCap.MemoryCapInMB,
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
                new SystemMemoryCapMock(),
                new Profiler(),
                MEMORY_THRESHOLD
            );

            public bool TrySpendBudget() =>
                performanceBudget.TrySpendBudget();

            private class SystemMemoryCapMock : ISystemMemoryCap
            {
                public long MemoryCapInMB { get; private set; } = 16 * 1024L;
                public int MemoryCap { set => MemoryCapInMB = value * 1024L; }
                public MemoryCapMode Mode { get; set; }
            }
        }
    }
}
