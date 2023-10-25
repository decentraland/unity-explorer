using ECS.Profiling;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
{
    public enum MemoryUsageStatus
    {
        Normal,
        Warning,
        Critical,
        Full,
    }

    public class MemoryBudgetProvider : IConcurrentBudgetProvider
    {
        private readonly IProfilingProvider profilingProvider;

        private readonly ISystemMemory systemMemory;

        public int RequestedMemoryCounter { get; private set; }

        private Dictionary<Type, int> memoryEstimationMap { get; }

        public MemoryBudgetProvider(Dictionary<Type, int> memoryEstimationMap, IProfilingProvider profilingProvider)
        {
            systemMemory = new SystemMemoryMock(5000);
            Debug.Log($"{systemMemory.TotalSizeInMB * 0.8} {systemMemory.TotalSizeInMB * 0.9}  {systemMemory.TotalSizeInMB * 0.95}");

            this.memoryEstimationMap = memoryEstimationMap;
            this.profilingProvider = profilingProvider;
        }

        public MemoryUsageStatus GetMemoryUsageStatus()
        {
            long usedMemory = profilingProvider.TotalUsedMemoryInBytes / ProfilingProvider.BYTES_IN_MEGABYTE;

            return usedMemory switch
                   {
                       _ when usedMemory > systemMemory.TotalSizeInMB * 0.95 => MemoryUsageStatus.Full,
                       _ when usedMemory > systemMemory.TotalSizeInMB * 0.9 => MemoryUsageStatus.Critical,
                       _ when usedMemory > systemMemory.TotalSizeInMB * 0.8 => MemoryUsageStatus.Warning,
                       _ => MemoryUsageStatus.Normal,
                   };
        }

        public float MemoryOverusageInMB()
        {
            long usedMemory = profilingProvider.TotalUsedMemoryInBytes * ProfilingProvider.BYTES_IN_MEGABYTE;
            float warningMemoryUsage = systemMemory.TotalSizeInMB * 0.8f;

            return usedMemory > warningMemoryUsage ? usedMemory - warningMemoryUsage : 0;
        }

        public bool TrySpendBudget<TAsset>()
        {
            // RequestedMemoryCounter += memoryEstimationMap[typeof(TAsset)];
            return TrySpendBudget();
        }

        public bool TrySpendBudget() =>
            GetMemoryUsageStatus() != MemoryUsageStatus.Full;

        public void ReleaseBudget() { }

        public void FlushRequestedMemoryCounter() =>
            RequestedMemoryCounter = 0;
    }
}
