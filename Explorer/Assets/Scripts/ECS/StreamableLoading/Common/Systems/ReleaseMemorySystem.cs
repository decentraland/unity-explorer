using Arch.Core;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.Groups;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using Global;
using UnityEngine;

namespace ECS.StreamableLoading.Common.Systems
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class ReleaseMemorySystem : BaseUnityLoopSystem
    {
        private readonly MemoryBudgetProvider memoryBudgetProvider;
        private readonly CacheCleaner cacheCleaner;

        private ReleaseMemorySystem(World world, CacheCleaner cacheCleaner, MemoryBudgetProvider memoryBudgetProvider) : base(world)
        {
            this.cacheCleaner = cacheCleaner;
            this.memoryBudgetProvider = memoryBudgetProvider;
        }

        protected override void Update(float t)
        {
            Debug.Log($"VV:: {memoryBudgetProvider.TrySpendBudget()} {memoryBudgetProvider.RequestedMemoryCounter}");

            switch (memoryBudgetProvider.GetMemoryUsageStatus())
            {
                case MemoryUsageStatus.Warning:
                    cacheCleaner.UnloadUnusedCache(memoryBudgetProvider.RequestedMemoryCounter);
                    break;
                case MemoryUsageStatus.Critical:
                    cacheCleaner.UnloadAllCache();
                    break;
            }

            // --- Unload scenes
            // 1. Far and Behind

            memoryBudgetProvider.FlushRequestedMemoryCounter();
        }
    }
}
