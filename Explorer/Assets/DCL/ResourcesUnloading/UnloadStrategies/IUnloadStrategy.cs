using DCL.Optimization.PerformanceBudgeting;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public interface IUnloadStrategy
    {
        void TryUnload(IMemoryUsageProvider memoryBudgetProvider, ICacheCleaner cacheCleaner);
    }
}