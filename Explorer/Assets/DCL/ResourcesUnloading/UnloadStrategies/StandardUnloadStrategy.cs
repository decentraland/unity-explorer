using DCL.Optimization.PerformanceBudgeting;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public class StandardUnloadStrategy : IUnloadStrategy
    {
        public void TryUnload(IMemoryUsageProvider memoryBudgetProvider, ICacheCleaner cacheCleaner)
        {
            if (memoryBudgetProvider.GetMemoryUsageStatus() != MemoryUsageStatus.NORMAL)
                cacheCleaner.UnloadCache();
        }
    }
}