using System;

namespace DCL.PerformanceBudgeting
{
    public interface IAcquiredBudget : IDisposable
    {
        void Release();
    }
}
