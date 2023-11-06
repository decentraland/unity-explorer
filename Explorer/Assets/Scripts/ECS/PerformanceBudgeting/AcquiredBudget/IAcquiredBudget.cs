using System;

namespace ECS.StreamableLoading.DeferredLoading.BudgetProvider
{
    public interface IAcquiredBudget : IDisposable
    {
        void Release();
    }
}
