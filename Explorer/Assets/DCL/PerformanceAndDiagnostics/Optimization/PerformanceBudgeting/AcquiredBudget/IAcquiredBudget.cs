using System;

namespace DCL.Optimization.PerformanceBudgeting
{
    public interface IAcquiredBudget : IDisposable
    {
        void Release();
    }
}
