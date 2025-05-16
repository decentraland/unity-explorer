using DCL.Profiling;
using System;
using System.Collections.Generic;
using UnityEngine;
using static DCL.Optimization.PerformanceBudgeting.MemoryUsageStatus;

namespace DCL.Optimization.PerformanceBudgeting
{
    public enum MemoryUsageStatus
    {
        ABUNDANCE,
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
        public bool SimulateLackOfAbundance;

        public MemoryBudget(ISystemMemoryCap systemMemoryCap, IBudgetProfiler profiler, IReadOnlyDictionary<MemoryUsageStatus, float> memoryThreshold)
        {
            SimulatedMemoryUsage = ABUNDANCE;

            this.systemMemoryCap = systemMemoryCap;
            this.profiler = profiler;
            this.memoryThreshold = memoryThreshold;
        }

        private MemoryUsageStatus GetMemoryUsageStatus()
        {
            long usedMemory = profiler.SystemUsedMemoryInBytes / BYTES_IN_MEGABYTE;
            long totalSystemMemory = GetTotalSystemMemoryInMB();

            return usedMemory switch
                   {
                       _ when usedMemory > totalSystemMemory * memoryThreshold[FULL] => FULL,
                       _ when usedMemory > totalSystemMemory * memoryThreshold[WARNING] => WARNING,
                       _ when usedMemory < totalSystemMemory * memoryThreshold[ABUNDANCE] => ABUNDANCE,
                       _ => NORMAL,
                   };
        }

        public (int warning, int full) GetMemoryRanges()
        {
            long totalSizeInMB = GetTotalSystemMemoryInMB();
            return ((int) (totalSizeInMB * memoryThreshold[WARNING]), (int)(totalSizeInMB * memoryThreshold[FULL]));
        }

        public bool TrySpendBudget() =>
            !IsMemoryFull();

        public long GetTotalSystemMemoryInMB()
        {
            return SimulatedMemoryUsage switch
                   {
                       FULL => NO_MEMORY,
                       WARNING => CalculateSystemMemoryForWarningThreshold(),
                       _ => systemMemoryCap.MemoryCapInMB,
                   };

            // ReSharper disable once PossibleLossOfFraction
            long CalculateSystemMemoryForWarningThreshold() => // Increase the threshold halfway between warning and full
                (long)(profiler.SystemUsedMemoryInBytes / BYTES_IN_MEGABYTE / (memoryThreshold[WARNING] * GetHalfwayBetweenLimits(FULL, WARNING)));

            float GetHalfwayBetweenLimits(MemoryUsageStatus upperLimit, MemoryUsageStatus bottomLimit) =>
                1 + ((memoryThreshold[upperLimit] - memoryThreshold[bottomLimit])/2f);
        }

        public bool IsInAbundance()
        {
            if (SimulateLackOfAbundance)
                return false;

            return GetMemoryUsageStatus() == ABUNDANCE;
        }

        public bool IsMemoryNormal()
        {
            MemoryUsageStatus status = GetMemoryUsageStatus();
            return status is NORMAL or ABUNDANCE;
        }

        public bool IsMemoryFull() =>
            GetMemoryUsageStatus() == FULL;

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
            }
        }
    }
}
