using Arch.Core;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.Groups;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;
using Global;

namespace ECS.StreamableLoading.Common.Systems
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class ReleaseMemorySystem : BaseUnityLoopSystem
    {
        private readonly MemoryBudgetProvider memoryBudgetProvider;
        private readonly CacheCleaner cacheCleaner;

        public ReleaseMemorySystem(World world, CacheCleaner cacheCleaner, MemoryBudgetProvider memoryBudgetProvider) : base(world)
        {
            this.cacheCleaner = cacheCleaner;
            this.memoryBudgetProvider = memoryBudgetProvider;
        }

        protected override void Update(float t)
        {
            // if (!memoryBudgetProvider.TrySpendBudget())                 cacheCleaner.UnloadCache();

            // --- Unload cache
            // 1. Unload long time not used cache
            // 2. Unload less used caches

            // --- Unload scenes
            // 1. Far and Behind

            memoryBudgetProvider.FlushRequestedMemoryCounter();
        }
    }
}
