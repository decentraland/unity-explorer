using DCL.Diagnostics;
using DCL.Profiling;
using System.Collections.Generic;
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
        private readonly float fullThreshold;
        private readonly float warningThreshold;
        private readonly float abundanceThreshold;

        public MemoryUsageStatus SimulatedMemoryUsage { private get; set; }
        public bool SimulateLackOfAbundance;

        private MemoryUsageStatus cachedStatus;
        private int cachedFrame = -1;

        public MemoryBudget(ISystemMemoryCap systemMemoryCap, IBudgetProfiler profiler, IReadOnlyDictionary<MemoryUsageStatus, float> memoryThreshold)
        {
            SimulatedMemoryUsage = Abundance;

            this.systemMemoryCap = systemMemoryCap;
            this.profiler = profiler;
            fullThreshold = GetRequiredThreshold(memoryThreshold, Full);
            warningThreshold = GetRequiredThreshold(memoryThreshold, Warning);

            // Without an Abundance threshold the budget never reports abundance
            abundanceThreshold = memoryThreshold.GetValueOrDefault(Abundance, 0f);
        }

        private static float GetRequiredThreshold(IReadOnlyDictionary<MemoryUsageStatus, float> memoryThreshold, MemoryUsageStatus status)
        {
            if (memoryThreshold.TryGetValue(status, out float threshold))
                return threshold;

            ReportHub.LogError(ReportCategory.ENGINE, $"Memory threshold for {status} is not configured, the {status} status is disabled");
            return float.MaxValue;
        }

        private MemoryUsageStatus GetMemoryUsageStatus()
        {
            if(UnityEngine.Time.frameCount == cachedFrame)
                return cachedStatus;

            long usedMemory = profiler.SystemUsedMemoryInBytes / BYTES_IN_MEGABYTE;
            long totalSystemMemory = GetTotalSystemMemoryInMB();

            cachedStatus = usedMemory switch
                           {
                               _ when usedMemory > totalSystemMemory * fullThreshold => Full,
                               _ when usedMemory > totalSystemMemory * warningThreshold => Warning,
                               _ when usedMemory < totalSystemMemory * abundanceThreshold => Abundance,
                               _ => Normal,
                           };

            cachedFrame = UnityEngine.Time.frameCount;
            return cachedStatus;
        }

        public (int warning, int full) GetMemoryRanges()
        {
            long totalSizeInMB = GetTotalSystemMemoryInMB();
            return ((int) (totalSizeInMB * warningThreshold), (int)(totalSizeInMB * fullThreshold));
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
                (long)(profiler.SystemUsedMemoryInBytes / BYTES_IN_MEGABYTE / (warningThreshold * GetHalfwayBetweenLimits()));

            float GetHalfwayBetweenLimits() =>
                1 + ((fullThreshold - warningThreshold)/2f);
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
