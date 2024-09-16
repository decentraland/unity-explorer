using System;
using DCL.Optimization.PerformanceBudgeting;

namespace DCL.ResourcesUnloading.UnloadStrategies
{
    public class AggressiveUnloadStrategy : IUnloadStrategy
    {
        public void TryUnload(IMemoryUsageProvider memoryBudgetProvider, ICacheCleaner cacheCleaner)
        {
            throw new NotImplementedException();
        }
    }
}