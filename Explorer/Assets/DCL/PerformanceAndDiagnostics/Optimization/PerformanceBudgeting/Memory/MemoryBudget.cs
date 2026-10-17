using DCL.Profiling;
using System;
using System.Collections.Generic;
using UnityEngine;
using static DCL.Optimization.PerformanceBudgeting.MemoryUsageStatus;

namespace DCL.Optimization.PerformanceBudgeting
{
    public enum MemoryUsageStatus
    {
        Abundance,
        Normal,
        Warning,
        Full,
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

        private MemoryUsageStatus cachedStatus;
        private int cachedFrame = -1;

        public MemoryBudget(ISystemMemoryCap systemMemoryCap, IBudgetProfiler profiler, IReadOnlyDictionary<MemoryUsageStatus, float> memoryThreshold)
        {
            SimulatedMemoryUsage = Abundance;

            this.systemMemoryCap = systemMemoryCap;
            this.profiler = profiler;
            this.memoryThreshold = memoryThreshold;
        }

        private MemoryUsageStatus GetMemoryUsageStatus()
        {
            if(UnityEngine.Time.frameCount == cachedFrame)
                return cachedStatus;

            long usedMemory = profiler.SystemUsedMemoryInBytes / BYTES_IN_MEGABYTE;
            long totalSystemMemory = GetTotalSystemMemoryInMB();

            cachedStatus = usedMemory switch
                           {
                               _ when usedMemory > totalSystemMemory * memoryThreshold[Full] => Full,
                               _ when usedMemory > totalSystemMemory * memoryThreshold[Warning] => Warning,
                               _ when usedMemory < totalSystemMemory * memoryThreshold[Abundance] => Abundance,
                               _ => Normal,
                           };

            cachedFrame = UnityEngine.Time.frameCount;
            return cachedStatus;
        }

        public (int warning, int full) GetMemoryRanges()
        {
            long totalSizeInMB = GetTotalSystemMemoryInMB();
            return ((int) (totalSizeInMB * memoryThreshold[Warning]), (int)(totalSizeInMB * memoryThreshold[Full]));
        }

        public bool TrySpendBudget() =>
            !IsMemoryFull();

        public long GetTotalSystemMemoryInMB()
        {
            return SimulatedMemoryUsage switch
                   {
                       Full => NO_MEMORY,
                       Warning => CalculateSystemMemoryForWarningThreshold(),
                       _ => systemMemoryCap.MemoryCapInMB,
                   };

            // ReSharper disable once PossibleLossOfFraction
            long CalculateSystemMemoryForWarningThreshold() => // Increase the threshold halfway between warning and full
                (long)(profiler.SystemUsedMemoryInBytes / BYTES_IN_MEGABYTE / (memoryThreshold[Warning] * GetHalfwayBetweenLimits(Full, Warning)));

            float GetHalfwayBetweenLimits(MemoryUsageStatus upperLimit, MemoryUsageStatus bottomLimit) =>
                1 + ((memoryThreshold[upperLimit] - memoryThreshold[bottomLimit])/2f);
        }

        public bool IsInAbundance()
        {
            if (SimulateLackOfAbundance)
                return false;

            return GetMemoryUsageStatus() == Abundance;
        }

        public bool IsMemoryNormal()
        {
            MemoryUsageStatus status = GetMemoryUsageStatus();
            return status is Normal or Abundance;
        }

        public bool IsMemoryFull() =>
            GetMemoryUsageStatus() == Full;

        public class Default : IPerformanceBudget
        {
            private static readonly IReadOnlyDictionary<MemoryUsageStatus, float> MEMORY_THRESHOLD = new Dictionary<MemoryUsageStatus, float>
            {
                { Warning, 0.65f },
                { Full, 0.75f }
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
