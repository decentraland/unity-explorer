#nullable enable

using DCL.Profiling;
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

    public class MemoryBudget : IMemoryUsageProvider, IPerformanceBudget
    {
        private const long BYTES_IN_MEGABYTE = 1024 * 1024;
        private const long NO_MEMORY = 0;

        private readonly IBudgetProfiler profiler;
        private readonly IReadOnlyDictionary<MemoryUsageStatus, float> memoryThreshold;

        // Debug
        private readonly bool isReleaseBuild = !Debug.isDebugBuild;
        public MemoryUsageStatus SimulatedMemoryUsage { private get; set; }

        internal long actualSystemMemory;

        public MemoryBudget(ISystemMemory systemMemory, IBudgetProfiler profiler, IReadOnlyDictionary<MemoryUsageStatus, float> memoryThreshold)
        {
            SimulatedMemoryUsage = Normal;
            this.profiler = profiler;
            this.memoryThreshold = memoryThreshold;
            actualSystemMemory = systemMemory.TotalSizeInMB;
        }

        public MemoryUsageStatus GetMemoryUsageStatus()
        {
            long usedMemory = profiler.SystemUsedMemoryInBytes / BYTES_IN_MEGABYTE;
            long totalSystemMemory = GetTotalSystemMemory();

            return usedMemory switch
                   {
                       _ when usedMemory > totalSystemMemory * memoryThreshold[Full] => Full,
                       _ when usedMemory > totalSystemMemory * memoryThreshold[Warning] => Warning,
                       _ => Normal,
                   };
        }

        public (float warning, float full) GetMemoryRanges()
        {
            long totalSizeInMB = GetTotalSystemMemory();
            return (totalSizeInMB * memoryThreshold[Warning], totalSizeInMB * memoryThreshold[Full]);
        }

        public bool TrySpendBudget() =>
            GetMemoryUsageStatus() != Full;

        private long GetTotalSystemMemory() =>
            isReleaseBuild ? actualSystemMemory : GetSimulatedSystemMemory();

        private long GetSimulatedSystemMemory()
        {
            return SimulatedMemoryUsage switch
                   {
                       Full => NO_MEMORY,
                       Warning => CalculateSystemMemoryForWarningThreshold(),
                       _ => actualSystemMemory
                   };

            // ReSharper disable once PossibleLossOfFraction
            long CalculateSystemMemoryForWarningThreshold() => // 10% higher than Warning threshold for current usedMemory
                (long)(profiler.SystemUsedMemoryInBytes / BYTES_IN_MEGABYTE / (memoryThreshold[Warning] * 1.1f));
        }

        public class Default : IPerformanceBudget
        {
            public static readonly IReadOnlyDictionary<MemoryUsageStatus, float> MEMORY_THRESHOLD = new Dictionary<MemoryUsageStatus, float>
            {
                { Warning, 0.8f },
                { Full, 0.95f },
            };

            private readonly IPerformanceBudget performanceBudget;

            public Default()
            {
                performanceBudget = new MemoryBudget(
                    new StandaloneSystemMemory(),
                    new Profiler(),
                    MEMORY_THRESHOLD
                );
            }

            public bool TrySpendBudget() =>
                performanceBudget.TrySpendBudget();
        }
    }
}
