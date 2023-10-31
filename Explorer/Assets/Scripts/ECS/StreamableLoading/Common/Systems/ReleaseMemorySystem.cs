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

        private ReleaseMemorySystem(World world, CacheCleaner cacheCleaner, MemoryBudgetProvider memoryBudgetProvider) : base(world)
        {
            this.cacheCleaner = cacheCleaner;
            this.memoryBudgetProvider = memoryBudgetProvider;
        }

        protected override void Update(float t)
        {
            switch (memoryBudgetProvider.GetMemoryUsageStatus())
            {
                case MemoryUsageStatus.Warning:
                    cacheCleaner.UnloadCache();
                    break;
                case MemoryUsageStatus.Critical:
                    cacheCleaner.UnloadCache();
                    break;
                case MemoryUsageStatus.Full:
                    cacheCleaner.UnloadCache();
                    break;
            }
        }
    }
}
