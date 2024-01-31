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
        private const ulong BYTES_IN_MEGABYTE = 1024 * 1024;
        private const ulong NO_MEMORY = 0;

        private readonly IProfilingProvider profilingProvider;
        private readonly IReadOnlyDictionary<MemoryUsageStatus, float> memoryThreshold;

        // Debug
        private readonly bool isReleaseBuild = !Debug.isDebugBuild;
        public MemoryUsageStatus SimulatedMemoryUsage { private get; set; }

        internal ulong ActualSystemMemory;

        public MemoryBudget(ISystemMemory systemMemory, IProfilingProvider profilingProvider, IReadOnlyDictionary<MemoryUsageStatus, float> memoryThreshold)
        {
            SimulatedMemoryUsage = Normal;
            this.profilingProvider = profilingProvider;
            this.memoryThreshold = memoryThreshold;
            ActualSystemMemory = systemMemory.TotalSizeInMB;
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

        private ulong GetTotalSystemMemory() =>
            isReleaseBuild ? ActualSystemMemory : GetSimulatedSystemMemory();

        private ulong GetSimulatedSystemMemory()
        {
            return SimulatedMemoryUsage switch
                   {
                       Full => NO_MEMORY,
                       Warning => CalculateSystemMemoryForWarningThreshold(),
                       _ => ActualSystemMemory
                   };

            // ReSharper disable once PossibleLossOfFraction
            ulong CalculateSystemMemoryForWarningThreshold() => // 10% higher than Warning threshold for current usedMemory
                (ulong)(profilingProvider.TotalUsedMemoryInBytes / BYTES_IN_MEGABYTE / (memoryThreshold[Warning] * 1.1f));
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
                    new ProfilingProvider(),
                    MEMORY_THRESHOLD
                );
            }

            public bool TrySpendBudget() =>
                performanceBudget.TrySpendBudget();
        }
    }
}
